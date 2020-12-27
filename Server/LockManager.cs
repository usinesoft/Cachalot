using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Client;
using Client.Tools;
using JetBrains.Annotations;

namespace Server
{
    public class LockManager : ILockManager
    {

        

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
        private readonly SafeDictionary<string, ReaderWriterLockSlim> _locksByCollection =
            new SafeDictionary<string, ReaderWriterLockSlim>(() =>
                new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion));

        /// <summary>
        ///     Currently active lock sessions
        /// </summary>
        private readonly SafeDictionary<Guid, Session> _activeSessions = new SafeDictionary<Guid, Session>(null);


        /// <summary>
        ///     Keep the timestamp of the locks currently taken
        /// </summary>
        private readonly SafeDictionary<ReaderWriterLockSlim, DateTime> _locksCurrentlyTaken =
            new SafeDictionary<ReaderWriterLockSlim, DateTime>(null);


        public long Retries { get; private set; }

        private ReaderWriterLockSlim Lock(string name)
        {
            return _locksByCollection.GetOrCreate(name);
        }

        private  void AcquireReadLock(Guid sessionId, params string[] collectionNames)
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


            HashSet<string> holdLocks = new HashSet<string>();

            
            foreach (var collectionName in resourceNames)
            {
                var @lock = Lock(collectionName);

                result = @lock.TryEnterReadLock(delayInMilliseconds);

                if (result)
                {
                    _locksCurrentlyTaken[@lock] = DateTime.Now;
                    holdLocks.Add(collectionName);
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

            if (!result)
            {
                if (holdLocks.Count > 0)
                {
                    RemoveReadLock(holdLocks.ToArray());
                }
            }
            else
            {
                foreach (var resourceName in resourceNames)
                {
                    Debug.Assert(_locksByCollection[resourceName].IsReadLockHeld);    
                }
            }

            return result;
        }

        public bool TryAcquireWriteLock(Guid sessionId, int delayInMilliseconds, params string[] resourceNames)
        {
            var result = true;

            HashSet<string> holdLocks = new HashSet<string>();

            
            foreach (var collectionName in resourceNames)
            {
                var @lock = Lock(collectionName);

                result = @lock. TryEnterWriteLock(delayInMilliseconds);

                if (result)
                {
                    _locksCurrentlyTaken[@lock] = DateTime.Now;
                    holdLocks.Add(collectionName);
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

            if (!result)
            {
                if (holdLocks.Count > 0)
                {
                    RemoveWriteLock(holdLocks.ToArray());
                }
                
            }
            else
            {
                foreach (var resourceName in resourceNames)
                {
                    Debug.Assert(_locksByCollection[resourceName].IsWriteLockHeld);    
                }
                
            }
            

            return result;
        }


        private void RemoveReadLock(params string[] resourceNames)
        {
            if (resourceNames.Length == 0)
                throw new ArgumentException("Value cannot be an empty collection.", nameof(resourceNames));

            
            foreach (var collectionName in resourceNames)
            {
                var @lock = Lock(collectionName);
                if (@lock.IsReadLockHeld)
                {
                    @lock.ExitReadLock();

                    if (@lock.RecursiveReadCount == 0)
                    {
                        _locksCurrentlyTaken.Remove(@lock);
                    }
                    
                }
            }
        

        }

        private void RemoveWriteLock(params string[] resourceNames)
        {
            
            foreach (var collectionName in resourceNames)
            {
                var @lock = Lock(collectionName);
                if (@lock.IsWriteLockHeld)
                {
                    @lock.ExitWriteLock();

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
                        if (@lock.IsReadLockHeld)
                            @lock.ExitReadLock();

                        if (@lock.IsWriteLockHeld)
                            @lock.ExitWriteLock();

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