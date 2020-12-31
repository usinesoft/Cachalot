using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Tools;
using JetBrains.Annotations;

namespace Server
{
    public class LockManager : ILockManager
    {
        /// <summary>
        ///     Like ReadWriteLOck (much simpler in fact) but not by-thread (as a session can be open by a thread and closed by
        ///     another)
        /// </summary>
        private class ServerSideLock
        {
            private int _writeCount;

            private bool _pendingWriteRequest;

            private readonly object _sync = new object();

            public int ReadCount { get; private set; }

            private static readonly int ProcessorCount = Environment.ProcessorCount;

            private static readonly int Retries = 100;

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
                }
            }

            /// <summary>
            ///     Only called by administrator
            /// </summary>
            public void ForceReset()
            {
                while (true)
                    lock (_sync)
                    {
                        ReadCount = 0;
                        _writeCount = 0;

                        break;
                    }
            }
        }


        private class Session
        {
            public Session(string[] resourceNames)
            {
                LockedResources = new HashSet<string>(resourceNames);
            }

            public HashSet<string> LockedResources { get; }

            public bool IsWriteLock { get; set; }
        }

        private readonly IEventsLog _eventLog;

        public LockManager(IEventsLog eventLog = null)
        {
            _eventLog = eventLog;
        }

        private const int DefaultWaitForLockInMilliseconds = 20;


        /// <summary>
        ///     One lock for each resource. Only add, never remove
        /// </summary>
        private readonly SafeDictionary<string, ServerSideLock> _locksByCollection =
            new SafeDictionary<string, ServerSideLock>(() =>
                new ServerSideLock());

        /// <summary>
        ///     Currently active lock sessions
        /// </summary>
        private readonly SafeDictionary<Guid, Session> _activeSessions = new SafeDictionary<Guid, Session>(null);


        /// <summary>
        ///     Keep the timestamp of the locks currently taken
        /// </summary>
        private readonly SafeDictionary<ServerSideLock, DateTime> _locksCurrentlyTaken =
            new SafeDictionary<ServerSideLock, DateTime>(null);


        public long Retries { get; private set; }

        private ServerSideLock Lock(string name)
        {
            return _locksByCollection.GetOrCreate(name);
        }

        private void AcquireReadLock(Guid sessionId, params string[] collectionNames)
        {
            Retries += LockPolicy.SmartRetry(() =>
                TryAcquireReadLock(sessionId, DefaultWaitForLockInMilliseconds, collectionNames));
        }

        private void AcquireWriteLock(Guid sessionId, params string[] collectionNames)
        {
            Retries += LockPolicy.SmartRetry(() =>
                TryAcquireWriteLock(sessionId, DefaultWaitForLockInMilliseconds, collectionNames));
        }


        public bool TryAcquireReadLock(Guid sessionId, int delayInMilliseconds, params string[] resourceNames)
        {
            var result = true;

            var lockedResources = new HashSet<string>();
            foreach (var collectionName in resourceNames)
            {
                var @lock = Lock(collectionName);

                result = @lock.TryEnterRead();

                if (result)
                {
                    lockedResources.Add(collectionName);
                    _locksCurrentlyTaken[@lock] = DateTime.Now;
                }

                if (!result)
                    break;
            }


            if (result && sessionId != default)
            {
                var session = _activeSessions.TryGetValue(sessionId);
                if (session == null)
                    _activeSessions.Add(sessionId, new Session(resourceNames));
                else
                    throw new NotSupportedException("The lock session has already been open");
            }

            if (!result && lockedResources.Any()) RemoveReadLock(lockedResources.ToArray());


            return result;
        }

        public bool TryAcquireWriteLock(Guid sessionId, int delayInMilliseconds, params string[] resourceNames)
        {
            var result = true;

            var lockedResources = new HashSet<string>();
            foreach (var collectionName in resourceNames)
            {
                var @lock = Lock(collectionName);


                result = @lock.TryEnterWrite();


                if (result)
                {
                    lockedResources.Add(collectionName);
                    _locksCurrentlyTaken[@lock] = DateTime.Now;
                }

                if (!result)
                    break;
            }


            if (result && sessionId != default)
            {
                var session = _activeSessions.TryGetValue(sessionId);
                if (session == null)
                    _activeSessions.Add(sessionId, new Session(resourceNames) {IsWriteLock = true});
                else
                    throw new NotSupportedException("The lock session has already been open");
            }

            if (!result && lockedResources.Any()) RemoveWriteLock(lockedResources.ToArray());


            return result;
        }


        //private readonly object _removeSync = new object();

        private void RemoveReadLock(params string[] resourceNames)
        {
            //lock (_removeSync)
            {
                if (resourceNames.Length == 0)
                    throw new ArgumentException("Value cannot be an empty collection.", nameof(resourceNames));


                foreach (var collectionName in resourceNames)
                {
                    var @lock = Lock(collectionName);

                    @lock.ExitRead();


                    if (@lock.ReadCount == 0) _locksCurrentlyTaken.Remove(@lock);
                }
            }
        }

        private void RemoveWriteLock(params string[] resourceNames)
        {
            //lock (_removeSync)
            {
                foreach (var collectionName in resourceNames)
                {
                    var @lock = Lock(collectionName);

                    @lock.ExitWrite();

                    _locksCurrentlyTaken.Remove(@lock);
                }
            }
        }


        #region interface implementation

        public void AcquireLock(Guid sessionId, bool writeAccess, params string[] resourceNames)
        {
            if (resourceNames.Length == 0)
                throw new ArgumentException("At least one resource name must be specified", nameof(resourceNames));

            if (writeAccess)
                AcquireWriteLock(sessionId, resourceNames);
            else
                AcquireReadLock(sessionId, resourceNames);
        }


        public bool CheckLock(Guid sessionId, bool writeAccess, params string[] resourceNames)
        {
            if (sessionId == default)
                throw new ArgumentException("Invalid sessionId");

            if (resourceNames.Length == 0)
                throw new ArgumentException(
                    "At least a collection name should be provided when checking that a lock is active",
                    nameof(resourceNames));

            var session = _activeSessions.TryGetValue(sessionId);

            if (session == null) return false;

            if (session.IsWriteLock != writeAccess) return false;

            return resourceNames.All(c => session.LockedResources.Contains(c));
        }

        public void CloseSession(Guid sessionId)
        {
            if (sessionId == default)
                throw new ArgumentException("Invalid sessionId");


            var session = _activeSessions.TryRemove(sessionId);

            if (session == null) throw new NotSupportedException("The session is not active");

            if (session.IsWriteLock)
                RemoveWriteLock(session.LockedResources.ToArray());
            else
                RemoveReadLock(session.LockedResources.ToArray());
        }

        public void DoWithReadLock([NotNull] Action action, params string[] resourceNames)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (resourceNames.Length == 0)
                throw new ArgumentException("Value cannot be an empty collection.", nameof(resourceNames));

            try
            {
                AcquireReadLock(default, resourceNames);

                action();
            }
            finally
            {
                RemoveReadLock(resourceNames);
            }
        }

        public void DoWithWriteLock([NotNull] Action action, params string[] resourceNames)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (resourceNames.Length == 0)
                throw new ArgumentException("Value cannot be an empty collection.", nameof(resourceNames));

            try
            {
                AcquireWriteLock(default, resourceNames);

                action();
            }
            finally
            {
                RemoveWriteLock(resourceNames);
            }
        }


        public int ForceRemoveAllLocks(int olderThanInMilliseconds)
        {
            try
            {
                if (olderThanInMilliseconds == 0)
                {
                    var count = _locksCurrentlyTaken.Count;

                    foreach (var @lock in _locksCurrentlyTaken.Keys)
                    {
                        @lock.ForceReset();

                        _locksCurrentlyTaken
                            .Remove(@lock); // with a safe dictionary no worry about collection modified 
                    }

                    return count;
                }
                else
                {
                    var count = 0;
                    var now = DateTime.Now;

                    foreach (var pair in _locksCurrentlyTaken.Pairs)
                        if ((now - pair.Value).TotalMilliseconds >= olderThanInMilliseconds)
                        {
                            _locksCurrentlyTaken.Remove(pair.Key);
                            pair.Key.ForceReset();

                            count++;
                        }

                    return count;
                }
            }
            finally
            {
                _eventLog?.LogEvent(EventType.LockRemoved);
            }
        }

        public int GetCurrentlyHoldLocks(int milliseconds = 0)
        {
            if (milliseconds == 0)
                return _locksCurrentlyTaken.Count;

            var ages = _locksCurrentlyTaken.Values;

            var count = 0;
            var now = DateTime.Now;

            foreach (var age in ages)
                if ((now - age).TotalMilliseconds >= milliseconds)
                    count++;

            return count;
        }

        #endregion
    }
}