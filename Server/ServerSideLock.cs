using System;
using System.Diagnostics;
using System.Threading;
using Client;

namespace Server
{
    public class ServerSideLock
    {
        private int _writeCount;

        private bool _pendingWriteRequest;

        private readonly object _sync = new object();

        /// <summary>
        /// Mostly for debugging
        /// </summary>
        private readonly int _id;

        private static int _lastId;

        public ServerSideLock()
        {
            Interlocked.Increment(ref _lastId);

            _id = _lastId;
        }

        public int ReadCount { get; private set; }

        private static readonly int ProcessorCount = Environment.ProcessorCount;

        private static readonly int Retries = 10;

        private static readonly int MaxReads = ProcessorCount;

        private static void SpinWait(int spinCount)
        {
            const int lockSpinCycles = 20;

            //Exponential back-off
            if (spinCount < 5 && ProcessorCount > 1)
                Thread.SpinWait(lockSpinCycles * spinCount);
            else
                Thread.Sleep(0);
        }


        public bool TryEnterWrite()
        {
            var result = false;

            for (var i = 0; i < Retries; i++)
            {
                lock (_sync)
                {
                    if (ReadCount > 0 || _writeCount > 0)
                    {
                        if (_writeCount == 0) // do not mark as pending write if there is already a write lock
                            _pendingWriteRequest = true;
                    }
                    else
                    {
                        _writeCount++;

                        _pendingWriteRequest = false;

                        result = true;

                        break;
                    }
                }

                SpinWait(4);
            }

            Debug.Assert(!result || _writeCount > 0);

            Dbg.Trace($"lock = {_id} write lock result = {result} readCount={ReadCount} writeCount = {_writeCount} pendingWrite = {_pendingWriteRequest} maxReads = {MaxReads}");

            return result;
        }

        public bool TryEnterRead()
        {
            var result = false;

            for (var i = 0; i < Retries; i++)
            {
                lock (_sync)
                {
                    if (_writeCount == 0 && !_pendingWriteRequest && ReadCount < MaxReads)
                    {
                        result = true;
                        ReadCount++;

                        break;
                    }
                }

                SpinWait(4);
            }

            Debug.Assert(result ? ReadCount > 0  && !_pendingWriteRequest:ReadCount >= 0);

            Dbg.Trace($"lock = {_id} read lock result = {result} readCount={ReadCount} writeCount = {_writeCount} pendingWrite = {_pendingWriteRequest} maxReads = {MaxReads}");

            return result;
        }

        public void ExitRead()
        {
            lock (_sync)
            {
                if (_writeCount > 0)
                    throw new NotSupportedException("Calling ExitRead() when a write lock is hold");

                if (ReadCount == 0)
                    throw new NotSupportedException("Calling ExitRead() when no read lock is hold");

                ReadCount--;

                Debug.Assert(ReadCount >= 0);

                Dbg.Trace($"lock = {_id} exit read readCount={ReadCount} writeCount = {_writeCount} pendingWrite = {_pendingWriteRequest}");
            }

                
        }

        public void ExitWrite()
        {
            lock (_sync)
            {
                if (ReadCount > 0)
                    throw new NotSupportedException("Calling ExitWrite() when o read lock is hold");

                if (_writeCount == 0)
                    throw new NotSupportedException("Calling ExitWrite() when no write lock is hold");

                _writeCount = 0;

                _pendingWriteRequest = false;

                Debug.Assert(_writeCount == 0);

                Dbg.Trace($"lock = {_id} exit write readCount={ReadCount} writeCount = {_writeCount} pendingWrite = {_pendingWriteRequest}");
            }

                
        }

        /// <summary>
        ///     Only called by administrator
        /// </summary>
        public void ForceReset()
        {
            lock (_sync)
            {
                ReadCount = 0;
                _writeCount = 0;

            }
        }
    }
}