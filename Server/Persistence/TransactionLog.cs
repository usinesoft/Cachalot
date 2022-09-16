using Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Server.Persistence
{
    /// <summary>
    ///     Append-only persistent transaction log
    /// </summary>
    public class TransactionLog : IDisposable
    {
        #region public interface

        public static readonly string LogFileName = "transaction_log.bin";


        public int PendingTransactionsCount
        {
            get
            {
                lock (_syncRoot)
                {
                    return _transactionQueue.Count;
                }
            }
        }

        public void Dispose()
        {
            _queueNotEmpty.Set();

            _sleepEvent.Set();

            lock (_syncRoot)
            {
                _transactionLog.Close();

                _transactionQueue.Clear();

                _disposed = true;
            }
        }

        public TransactionLog(string workingDirectory = null)
        {
            if (workingDirectory == null)
            {
                LogFilePath = Path.Combine(Constants.DataPath, LogFileName);
                WorkinDir = Constants.DataPath;
            }
            else
            {
                var dir = Path.Combine(workingDirectory, Constants.DataPath);
                LogFilePath = Path.Combine(dir, LogFileName);

                WorkinDir = workingDirectory;
            }


            lock (_syncRoot)
            {
                if (File.Exists(LogFilePath))
                {
                    _transactionLog = new FileStream(LogFilePath, FileMode.Open);


                    LoadAllTransactions();
                }
                else
                {
                    CreateNewLog();
                }
            }
        }

        public string WorkinDir { get; }

        private string LogFilePath { get; }


        /// <summary>
        ///     Can be called only if all the contained transactions are in Processed status
        /// </summary>
        public void ClearLog()
        {
            lock (_syncRoot)
            {
                if (_disposed) throw new ObjectDisposedException("The object was disposed");

                if (_transactionQueue.Count != 0)
                    throw new NotSupportedException("Can not clear log if there are still transactions to process");

                _transactionLog.Close();

                File.Delete(LogFilePath);

                CreateNewLog();
            }
        }


        /// <summary>
        ///     Mostly for testing
        /// </summary>
        /// <returns></returns>
        public string FileAsString()
        {
            lock (_syncRoot)
            {
                var transactionLog = new FileStream(LogFilePath, FileMode.Open);
                try
                {
                    transactionLog.Seek(0, SeekOrigin.Begin);

                    var reader = new BinaryReader(transactionLog);

                    var lastOffset = reader.ReadInt64();

                    var sb = new StringBuilder();
                    sb.AppendLine($"LastOffset = {lastOffset}");

                    var completeTransaction = true;

                    try
                    {
                        while (true)
                        {
                            sb.AppendLine("----------------------");
                            var status = reader.ReadInt32();

                            completeTransaction = false;

                            var timestamp = reader.ReadInt64();
                            var length = reader.ReadInt32();
                            var data = reader.ReadBytes(length);
                            var offset = reader.ReadInt64();
                            var id = reader.ReadInt64();
                            var delay = reader.ReadInt32();
                            var endMarker = reader.ReadInt64();

                            if (endMarker != EndMarker)
                                throw new FormatException(
                                    "Inconsistent transaction log. End marker not found at the end of a transaction");

                            completeTransaction = true;

                            var time = new DateTime(timestamp);


                            var transaction = new TransactionData
                            {
                                TransactionStatus = (TransactionStatus)status,
                                TimeStamp = time,
                                Data = data,
                                Offset = offset,
                                Id = id,
                                DelayInMilliseconds = delay
                            };

                            sb.AppendLine(transaction.ToString());

                            if (transaction.TransactionStatus != TransactionStatus.Processed)
                                _transactionQueue.Enqueue(transaction);
                        }
                    }
                    catch (EndOfStreamException)
                    {
                        if (!completeTransaction)
                            throw new FormatException(
                                "Inconsistent transaction log. End of file reached in the middle of a transaction");
                    }

                    return sb.ToString();
                }
                finally
                {
                    transactionLog.Dispose();
                }
            }
        }

        /// <summary>
        ///     Cancel the last transaction. Works only if it was a delayed transaction
        /// </summary>
        public void CancelTransaction()
        {
            lock (_syncRoot)
            {
                Dbg.Trace("canceling transaction from transaction log");

                if (_disposed) throw new ObjectDisposedException("The object was disposed");

                // that can happen in conditional updates when the condition is not satisfied
                if (_transactionQueue.Count == 0) return;

                Dbg.Trace("begin canceling transaction");
                var data = _transactionQueue.Dequeue();

                Dbg.Trace("removed from queue");

                // reset the event so the next call is blocking
                if (_transactionQueue.Count == 0) _queueNotEmpty.Reset();

                if (data.DelayInMilliseconds == 0)
                    throw new NotSupportedException(
                        "Trying to cancel a transaction which is not delayed");

                data.TransactionStatus = TransactionStatus.Canceled;
                UpdateTransactionStatus(data);
                Dbg.Trace("end canceling transaction");
            }
        }

        public void NewTransaction(byte[] data, int delayInMilliseconds = 0)
        {
            lock (_syncRoot)
            {
                if (_disposed) throw new ObjectDisposedException("The object was disposed");


                // the file pointer is always at the end
                var transaction = new TransactionData
                {
                    TimeStamp = DateTime.Now,
                    TransactionStatus = TransactionStatus.ToProcess,
                    Offset = _lastOffset,
                    Data = data,
                    DelayInMilliseconds = delayInMilliseconds
                };


                var writer = new BinaryWriter(_transactionLog);

                writer.Write((int)transaction.TransactionStatus);
                writer.Write(transaction.TimeStamp.Ticks);
                writer.Write(transaction.Data.Length);
                writer.Write(transaction.Data);
                writer.Write(transaction.Offset);
                writer.Write(transaction.Id);
                writer.Write(transaction.DelayInMilliseconds);
                writer.Write(EndMarker);


                var newPosition = _transactionLog.Position;

                writer.Seek(0, SeekOrigin.Begin);

                writer.Write(newPosition);

                _lastOffset = newPosition;

                writer.Flush();

                _transactionLog.Seek(_lastOffset, SeekOrigin.Begin);

                _transactionQueue.Enqueue(transaction);

                _queueNotEmpty.Set();

                Dbg.Trace("new durable transaction in transaction log");
            }
        }

        public IPersistentTransaction StartProcessing()
        {
            _queueNotEmpty.WaitOne();


            if (_disposed) return null; // to avoid a race condition between the disposal of consumer and the log

            Dbg.Trace("start processing transaction from transaction log");

            // the event was set during dispose to unlock the consumer
            if (_transactionQueue.Count == 0) return null;


            TransactionData data;

            lock (_syncRoot)
            {
                if (_transactionQueue.Count == 0) return null;
                data = _transactionQueue.Peek();
            }

            // if it is a delayed transaction wait until it is valid
            if (data.DelayInMilliseconds != 0)
            {
                Dbg.Trace("processing delayed transaction");

                var millisecondsToWait =
                    data.DelayInMilliseconds - (int)(DateTime.Now - data.TimeStamp).TotalMilliseconds;
                if (millisecondsToWait > 0 && data.TransactionStatus != TransactionStatus.Canceled)
                {
                    Dbg.Trace($"waiting {millisecondsToWait} ms for delayed transaction to become active");
                    SmartSleep(millisecondsToWait);
                }
            }

            // if canceled do not process
            if (data.TransactionStatus == TransactionStatus.Canceled)
            {
                Dbg.Trace("delayed transaction was canceled");
                return null;
            }

            if (data.TransactionStatus != TransactionStatus.ToProcess)
                Dbg.Trace("Incomplete transaction found. Reprocessing");

            lock (_syncRoot)
            {
                data.TransactionStatus = TransactionStatus.Processing;
                UpdateTransactionStatus(data);
            }

            Dbg.Trace("processing delayed transaction after delay has passed");

            return data;
        }


        private readonly ManualResetEvent _sleepEvent = new ManualResetEvent(false);

        private void SmartSleep(int millisecondsToWait)
        {
            _sleepEvent.WaitOne(millisecondsToWait);
        }

        public void EndProcessing(IPersistentTransaction transaction)
        {
            Dbg.Trace("end processing transaction from transaction log");

            lock (_syncRoot)
            {
                if (_disposed) throw new ObjectDisposedException("The object was disposed");

                var data = _transactionQueue.Dequeue();

                // reset the event so the next call is blocking
                if (_transactionQueue.Count == 0) _queueNotEmpty.Reset();

                if (data.Id != transaction.Id || data.TransactionStatus != TransactionStatus.Processing)
                    throw new NotSupportedException(
                        "EndProcessing() can be called only on the one transaction currently in Processing status ");

                data.TransactionStatus = TransactionStatus.Processed;
                UpdateTransactionStatus(data);
            }
        }

        #endregion

        #region implementation

        /// <summary>
        ///     Internal data for a persistent transaction
        /// </summary>
        public class TransactionData : IPersistentTransaction
        {
            private static long _lastId = 1;


            public TransactionData()
            {
                Id = Interlocked.Increment(ref _lastId);
            }

            public long Offset { get; internal set; }

            /// <summary>
            ///     Used for delayed transaction in two stage mode
            /// </summary>
            public int DelayInMilliseconds { get; set; }

            public byte[] Data { get; internal set; }

            public DateTime TimeStamp { get; internal set; }

            public TransactionStatus TransactionStatus { get; internal set; }

            public long Id { get; set; }
            public bool EndMarkerOk { get; set; }


            public override string ToString()
            {
                var status = "UNKNOWN";


                switch (TransactionStatus)
                {
                    case TransactionStatus.ToProcess:
                        status = "TO_PROCESS";
                        break;
                    case TransactionStatus.Processing:
                        status = "PROCESSING";
                        break;
                    case TransactionStatus.Processed:
                        status = "PROCESSED ";
                        break;
                    case TransactionStatus.Canceled:
                        status = "CANCELED  ";
                        break;

                }


                return
                    $"Offset: {Offset}, Id: {Id} DataLength: {Data.Length}, TimeStamp: {TimeStamp}, Status: {status}, Delay: {DelayInMilliseconds}";
            }
        }

        private void CreateNewLog()
        {
            if (!Directory.Exists(WorkinDir)) Directory.CreateDirectory(WorkinDir);

            _transactionLog = new FileStream(LogFilePath, FileMode.CreateNew);

            _lastOffset = sizeof(long);
            var writer = new BinaryWriter(_transactionLog);
            writer.Write(_lastOffset);
            writer.Flush();
        }

        public static TransactionData ReadTransaction(BinaryReader reader)
        {
            var status = reader.ReadInt32();
            var timestamp = reader.ReadInt64();
            var length = reader.ReadInt32();
            var data = reader.ReadBytes(length);
            var offset = reader.ReadInt64();
            var id = reader.ReadInt64();
            var delay = reader.ReadInt32();
            var endMarker = reader.ReadInt64();



            var time = new DateTime(timestamp);


            var transaction = new TransactionData
            {
                TransactionStatus = (TransactionStatus)status,
                TimeStamp = time,
                Data = data,
                Offset = offset,
                Id = id,
                DelayInMilliseconds = delay,
                EndMarkerOk = endMarker == EndMarker
            };

            return transaction;
        }

        private void LoadAllTransactions()
        {
            _transactionLog.Seek(0, SeekOrigin.Begin);

            var reader = new BinaryReader(_transactionLog);

            _lastOffset = reader.ReadInt64();



            try
            {
                while (true)
                {



                    var transaction = ReadTransaction(reader);


                    if (!transaction.EndMarkerOk)
                        throw new FormatException(
                            "Inconsistent transaction log. End marker not found at the end of a transaction");


                    if (transaction.TransactionStatus != TransactionStatus.Processed &&
                        transaction.TransactionStatus != TransactionStatus.Canceled)
                        _transactionQueue.Enqueue(transaction);
                }
            }

            catch (EndOfStreamException)
            {
                // ignore (end of log)
            }


            if (_transactionQueue.Count > 0)
            {
                // only the last transaction may be in "Processing" status. In this case there was a failure during transaction processing
                // we mark it "ToProcess". Transactions are idempotent
                var lastTransaction = _transactionQueue.Peek();
                if (lastTransaction.TransactionStatus == TransactionStatus.Processing)
                {
                    ServerLog.LogWarning("Reprocessing incomplete transaction");

                    lastTransaction.TransactionStatus = TransactionStatus.ToProcess;
                    UpdateTransactionStatus(lastTransaction);
                }

                _queueNotEmpty.Set();
            }
        }

        private void UpdateTransactionStatus(TransactionData transaction)
        {
            lock (_syncRoot)
            {
                if (_disposed) return;

                _transactionLog.Position = transaction.Offset;
                var writer = new BinaryWriter(_transactionLog);

                writer.Write((int)transaction.TransactionStatus);

                writer.Flush();

                // move back at the end
                _transactionLog.Position = _lastOffset;


            }
        }

        #endregion

        #region private data

        /// <summary>
        ///     Synchronization object
        /// </summary>
        private readonly object _syncRoot = new object();


        private readonly Queue<TransactionData> _transactionQueue = new Queue<TransactionData>();

        private Stream _transactionLog;


        private long _lastOffset;


        private bool _disposed;

        /// <summary>
        ///     Internally used 'end of transaction' marker
        /// </summary>
        private static readonly long EndMarker = 0xABCD;

        private readonly ManualResetEvent _queueNotEmpty = new ManualResetEvent(false);

        #endregion
    }
}