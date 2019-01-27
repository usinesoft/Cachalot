using System;
using System.IO;
using System.Threading;
using Client;
using Client.Core;
using Client.Interface;

namespace Server.Persistence
{
    public class PersistenceEngine
    {
        ReliableStorage _storage;

        private Thread _singleConsumer;

        private TransactionLog TransactionLog { get; set; }


        private volatile bool _shouldContinue = true;
        

        public void WaitForPendingTransactions()
        {
            Dbg.Trace($"{TransactionLog.PendingTransactionsCount} pending transactions");

            while (TransactionLog.PendingTransactionsCount > 0)
            {
                Thread.Sleep(10);
            }
        }


        public void Start()
        {

            _storage = Container != null
                ? new ReliableStorage(new ObjectProcessor(Container), WorkingDirectory)
                : new ReliableStorage(new NullObjectProcessor(), WorkingDirectory);

            
            ServerLog.LogInfo("start processing schema");

            // Stage 1  load the schema and
            // register types (this will create an in-memory data store for each type)

            lock (_schemaSync)
            {
                Container?.LoadSchema(SchemaFilePath);
                Container?.LoadSequence(SequenceFilePath);
            }

            ServerLog.LogInfo("end processing schema");

            ServerLog.LogInfo("start loading transaction log");
            TransactionLog = new TransactionLog(WorkingDirectory);
            ServerLog.LogInfo("end loading transaction log");

            // if there are pending transactions data needs to be loaded twice
            // first without processing the persistent objects; simply initializing offset tables
            // second after processing pending transactions reload and store objects in memory
            if (TransactionLog.PendingTransactionsCount > 0)
            {
                ServerLog.LogInfo("start loading data before transaction processing");
                // load once without processing the objects
                _storage.LoadPersistentData(false);
                ServerLog.LogInfo("end loading data before transaction processing");
            }

            // start the thread that will process pending transactions
            StartProcessingTransactions();

            ServerLog.LogInfo("start waiting for pending transactions to be processed");
            // Stage 2 wait for the persistence engine to commit all the pending transactions in the log
            // in the storage


            WaitForPendingTransactions();

            ServerLog.LogInfo("end waiting for pending transactions");

            // Stage 3 house keeping

            ServerLog.LogInfo("start compacting the transaction log");
            TransactionLog.ClearLog();
            ServerLog.LogInfo("end compacting the transaction log");

            ServerLog.LogInfo("start compacting the persistence storage");
            _storage.CleanStorage();
            ServerLog.LogInfo("end compacting the persistence storage");

            ServerLog.LogInfo("begin load data");
            // Stage 4 load data from persisent storage to memory
            _storage.LoadPersistentData();
            ServerLog.LogInfo("end load data");
        }


        /// <summary>
        /// Restart after some administrative tasks that do not need data to be loaded from persistent storage
        /// </summary>
        public void LightStart()
        {
            _storage =  new ReliableStorage(new NullObjectProcessor(), WorkingDirectory);
            TransactionLog = new TransactionLog(WorkingDirectory);

            StartProcessingTransactions();
        }

        

        private void StartProcessingTransactions()
        {
            _shouldContinue = true;
            _singleConsumer = new Thread(() =>
            {
                while (_shouldContinue)
                {
                    try
                    {
                        // this call is blocking if nothing to process
                        var persistentTransaction = TransactionLog.StartProcessing();

                        if (persistentTransaction != null && persistentTransaction.TransactionStatus != TransactionStaus.Canceled)
                        {
                            var data = persistentTransaction.Data;
                            var transaction =
                                SerializationHelper.ObjectFromBytes<Transaction>(data, SerializationMode.ProtocolBuffers,
                                    false);

                            if (transaction is MixedTransaction mixedTransaction)
                            {
                                foreach (var item in mixedTransaction.ItemsToPut)
                                {
                                    var itemData =
                                        SerializationHelper.ObjectToBytes(item, SerializationMode.ProtocolBuffers, null);

                                    Dbg.Trace(
                                        $"storing persistent block for object {item} transaction={persistentTransaction.Id}");
                                    _storage.StoreBlock(itemData, item.GlobalKey,
                                        unchecked((int)persistentTransaction.Id));
                                }

                                foreach (var item in mixedTransaction.ItemsToDelete)
                                {
                                    Dbg.Trace($"deleting persistent block {persistentTransaction.Id}");
                                    _storage.DeleteBlock(item.GlobalKey, unchecked((int)persistentTransaction.Id));
                                }
                            }

                            if (transaction is PutTransaction putTransaction)
                            {
                                foreach (var item in putTransaction.Items)
                                {
                                    var itemData =
                                        SerializationHelper.ObjectToBytes(item, SerializationMode.ProtocolBuffers, null);

                                    Dbg.Trace(
                                        $"storing persistent block for object {item} transaction={persistentTransaction.Id}");
                                    _storage.StoreBlock(itemData, item.GlobalKey,
                                        unchecked ((int) persistentTransaction.Id));
                                }
                            }

                            if (transaction is DeleteTransaction deleteTransaction)
                            {
                                foreach (var item in deleteTransaction.ItemsToDelete)
                                {
                                    Dbg.Trace($"deleting persistent block {persistentTransaction.Id}");
                                    _storage.DeleteBlock(item.GlobalKey, unchecked ((int) persistentTransaction.Id));
                                }
                            }

                            TransactionLog.EndProcessing(persistentTransaction);
                        }
                        else
                        {
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        Dbg.Trace(e.Message);
                        //TODO add proper logging
                    }
                }
            });

            _singleConsumer.Start();
        }

        public void NewTransaction(Transaction transaction, bool isDelayed = false)
        {
            var delayInMilliseconds = isDelayed ? Constants.DelayForTwoStageTransactionsInMilliseconds : 0;

            try
            {

                TransactionLog.NewTransaction(
                    SerializationHelper.ObjectToBytes(transaction, SerializationMode.ProtocolBuffers, null), delayInMilliseconds);
            }
            catch (Exception e)
            {
                throw new CacheException(e.Message, ExceptionType.ErrorWritingDataInTransactionLog);
            }
        }


        public void Stop()
        {
            _shouldContinue = false;
            TransactionLog.Dispose();

            _singleConsumer.Join();


            _storage.Dispose();
        }


        public void StoreDataForRollback()
        {
            if (Directory.Exists(RollbackDataPath))
            {
                Directory.Delete(RollbackDataPath, true);
            }

            Directory.Move(DataPath, RollbackDataPath);
        }

        public void RollbackData()
        {
            if (Directory.Exists(DataPath))
            {
                Directory.Delete(DataPath, true);
            }

            Directory.Move(RollbackDataPath, DataPath);
        }

        public void DeleteRollbackData()
        {
            if (Directory.Exists(RollbackDataPath))
            {
                Directory.Delete(RollbackDataPath, true);
            }
        }

        readonly object _schemaSync = new object();
        readonly object _sequenceSync = new object();
        

        private string DataPath => WorkingDirectory != null? Path.Combine(WorkingDirectory, Constants.DataPath):Constants.DataPath;
        private string RollbackDataPath => WorkingDirectory != null? Path.Combine(WorkingDirectory, Constants.RollbackDataPath):Constants.RollbackDataPath;

        public PersistenceEngine(DataContainer dataContainer = null, string workingDirectory = null)
        {
            Container = dataContainer;
            WorkingDirectory = workingDirectory;
            
            SchemaFilePath = Path.Combine(DataPath, Constants.SchemaFileName);
            SequenceFilePath = Path.Combine(DataPath, Constants.SequenceFileName);
            
        }

        private string SchemaFilePath { get; }
        private string SequenceFilePath { get; }

        public DataContainer Container { get; set; }
        public string WorkingDirectory { get; }


        public void UpdateSchema(string schemaJson)
        {
            lock (_schemaSync)
            {
                File.WriteAllText(SchemaFilePath, schemaJson);
            }
        }


        public void UpdateSequences(string json)
        {

            lock (_sequenceSync)
            {
                File.WriteAllText(SequenceFilePath, json);
            }
        }

        public void CancelDelayedTransaction()
        {
            TransactionLog.CancelTransaction();
        }
    }
}