using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Tools;
using JetBrains.Annotations;

namespace Server
{
    public class LockManager:ILockManager
    {

        class Session
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

        private const int DefaultWaitForLockInMilliseconds = 10;

        readonly ReaderWriterLockSlim _globalLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        readonly SafeDictionary<string, ReaderWriterLockSlim> _locksByCollection = new SafeDictionary<string, ReaderWriterLockSlim>(()=> new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion) );
        readonly SafeDictionary<Guid, Session> _activeSessions = new SafeDictionary<Guid, Session>(null );


        /// <summary>
        /// Keep the timestamp of the locks currently taken
        /// </summary>
        readonly SafeDictionary<ReaderWriterLockSlim, DateTime> _locksCurrentlyTaken = new SafeDictionary<ReaderWriterLockSlim, DateTime>(null);

        public long SuccessfulReads => _successfulReads;

        public long SuccessfulWrites => _successfulWrites;

        public long Retries => _retries;

        ReaderWriterLockSlim Lock(string name)
        {
            return name == null ? _globalLock : _locksByCollection.GetOrCreate(name);
        }


        #region statiscics

        private long _successfulReads;
        private long _successfulWrites;
        private long _retries;
        


        #endregion

        private void AcquireReadLock(string collectionName = null)
        {
            var @lock = Lock(collectionName);
            SmartRetry(() => @lock.TryEnterReadLock(DefaultWaitForLockInMilliseconds));
            
            _locksCurrentlyTaken[@lock] =  DateTime.Now;

        }

        private void RemoveReadLock(string collectionName = null)
        {
            var @lock = Lock(collectionName);
            @lock.ExitReadLock();
            
            _locksCurrentlyTaken.Remove(@lock);
        }

        private void AcquireWriteLock(string collectionName = null)
        {
            var @lock = Lock(collectionName);
            SmartRetry(() => @lock.TryEnterWriteLock(DefaultWaitForLockInMilliseconds));
            
            _locksCurrentlyTaken[@lock] =  DateTime.Now;
        }

        private void RemoveWriteLock(string collectionName = null)
        {
            var @lock = Lock(collectionName);
            @lock.ExitWriteLock();

            _locksCurrentlyTaken.Remove(@lock);
        }

        public bool TryAcquireReadLock(Guid sessionId, int delayInMilliseconds, params string[] resourceNames)
        {
            bool result = true;

            try
            {
                foreach (var collectionName in resourceNames)
                {
                    var @lock = Lock(collectionName);

                    result = @lock.TryEnterReadLock(delayInMilliseconds);

                    if(result)
                        _locksCurrentlyTaken[@lock] =  DateTime.Now;

                    if(!result )
                        break;
                }
            }
            catch (LockRecursionException )
            {
                result = false;
            }

            if (result && sessionId != default)
            {
                _activeSessions.Add(sessionId, new Session(resourceNames));
            }

            return result;
        }

        public bool CheckReadLockIsActive(Guid sessionId)
        {
            if(sessionId == default)
                throw new ArgumentException("Invalid sessionId");

            return _activeSessions.ContainsKey(sessionId);

        }

        public void CloseSession(Guid sessionId)
        {
            if(sessionId == default)
                throw new ArgumentException("Invalid sessionId");

            
            var session = _activeSessions.TryRemove(sessionId);

            if (session == null)
            {
                throw new NotSupportedException("The session is not active");
            }

            RemoveReadLock(session.LockedResources.ToArray());

        }

        public void RemoveReadLock(params string[] resourceNames)
        {
            foreach (var collectionName in resourceNames)
            {
                var @lock = Lock(collectionName);
                if (@lock.IsReadLockHeld)
                {
                    @lock.ExitReadLock();

                    _locksCurrentlyTaken.Remove(@lock);
                }
            }

        }

        public bool TryAcquireWriteLock(int delayInMilliseconds, params string[] resourceNames)
        {
            bool result = true;

            foreach (var collectionName in resourceNames)
            {
                var @lock = Lock(collectionName);

                result = @lock.TryEnterWriteLock(delayInMilliseconds);

                if(result)
                    _locksCurrentlyTaken[@lock] =  DateTime.Now;

                if(!result )
                    break;
            }


            return result;
        }

        public void RemoveWriteLock(params string[] resourceNames)
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


        private  void SmartRetry(Func<bool> action, int maxRetry = 0)
        {
            int iteration = 0;
            while (true)
            {
                if (action())
                    break;

                iteration++;

                Interlocked.Increment(ref _retries);

                if (maxRetry > 0 && iteration >= maxRetry)
                    break;

                // this heuristic took lots of tests to nail down; it is a compromise between 
                // wait time for one client and average time for all clients
                var delay = ThreadLocalRandom.Instance.Next(10 * iteration % 5);
                
                Thread.Sleep(delay);
            }

        }


        #region interface implementation

        public void DoWithReadLock([NotNull] Action action, string resourceName = null)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            try
            {
                AcquireReadLock(resourceName);

                action();
            }
            finally
            {
                RemoveReadLock(resourceName);
            }
        }

        public void DoWithWriteLock(Action action, string resourceName = null)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            try
            {
                AcquireWriteLock(resourceName);

                action();
            }
            finally
            {
                RemoveWriteLock(resourceName);
            }
        }

        public bool DoIfReadLock(Action action, int delayInMilliseconds, params string[] resourceNames)
        {
            if (resourceNames.Length == 0)
                throw new ArgumentException("Value cannot be an empty collection.", nameof(resourceNames));
            try
            {
                if (TryAcquireReadLock(default, delayInMilliseconds, resourceNames))
                {
                    action();

                    Interlocked.Increment(ref _successfulReads);
                    return true;
                }

                return false;
            }
            finally
            {
                RemoveReadLock(resourceNames);
            }
        }

        public bool DoIfWriteLock(Action action, int delayInMilliseconds, params string[] resourceNames)
        {
            if (resourceNames.Length == 0)
                throw new ArgumentException("Value cannot be an empty collection.", nameof(resourceNames));
            try
            {
                if (TryAcquireWriteLock(delayInMilliseconds, resourceNames))
                {
                    action();

                    Interlocked.Increment(ref _successfulWrites);
                    return true;
                }

                return false;
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
                    var count =  _locksCurrentlyTaken.Count;
                
                    foreach (var @lock in _locksCurrentlyTaken.Keys)
                    {
                        if(@lock.IsReadLockHeld)
                            @lock.ExitReadLock();

                        if(@lock.IsWriteLockHeld)
                            @lock.ExitWriteLock();

                        _locksCurrentlyTaken.Remove(@lock);// with a safe dictionary no worry about collection modified 
                    }

                    return count;
                }
                else
                {
                    int count = 0;
                    var now = DateTime.Now;

                    foreach (var pair in _locksCurrentlyTaken.Pairs)
                    {
                        if ((now - pair.Value).TotalMilliseconds >= olderThanInMilliseconds)
                        {
                            _locksCurrentlyTaken.Remove(pair.Key);
                            count++;
                        }
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

            int count = 0;
            var now = DateTime.Now;

            foreach (var age in ages)
            {
                if ((now - age).TotalMilliseconds >= milliseconds)
                {
                    count++;
                }
            }

            return count;
        }

        #endregion
    }


    public interface ILockManager
    {

        /// <summary>
        /// Acquire read lock and run <see cref="Action"/> then release the read lock.
        /// If a resource is specified, the action concerns the named resource.
        /// Otherwise it is a global one.
        /// </summary>
        /// <param name="action">action to be executed inside lock</param>
        /// <param name="resourceName"></param>
        void DoWithReadLock(Action action,  string resourceName = null);


        /// <summary>
        /// Acquire write lock and run <see cref="Action"/> then release the write lock.
        /// If a resource is specified, the action concerns the named resource.
        /// Otherwise it is a global one.
        /// </summary>
        /// <param name="action">action to be executed inside lock</param>
        /// <param name="resourceName"></param>
        void DoWithWriteLock(Action action,  string resourceName = null);


        /// <summary>
        /// Try to acquire a read lock on all named resources during <see cref="delayInMilliseconds"/>
        /// If successful, run the action and then release the locks. Returns true if all the locks have been acquired and the action was executed
        /// </summary>
        /// <param name="action"></param>
        /// <param name="delayInMilliseconds"></param>
        /// <param name="resourceNames"></param>
        /// <returns></returns>
        bool DoIfReadLock(Action action, int delayInMilliseconds,  params string[] resourceNames);

        /// <summary>
        /// Try to acquire a write lock on all named resources during <see cref="delayInMilliseconds"/>
        /// If successful, run the action and then release the locks. Returns true if all the locks have been acquired and the action was executed
        /// </summary>
        /// <param name="action"></param>
        /// <param name="delayInMilliseconds"></param>
        /// <param name="resourceNames"></param>
        /// <returns></returns>
        bool DoIfWriteLock(Action action, int delayInMilliseconds,  params string[] resourceNames);


        bool TryAcquireReadLock(Guid sessionId, int delayInMilliseconds, params string[] resourceNames);
        
        void RemoveReadLock(params string[] resourceNames);
        
        bool TryAcquireWriteLock(int delayInMilliseconds, params string[] resourceNames);
        
        void RemoveWriteLock(params string[] resourceNames);

        /// <summary>
        /// Forcibly remove all locks that are currently hold for more than the specified timespan
        /// </summary>
        /// <returns></returns>
        int ForceRemoveAllLocks(int olderThanInMilliseconds);


        /// <summary>
        /// Returns the number of currently hold locks that are older than the specified timespan (all if 0)
        /// </summary>
        /// <param name="milliseconds"></param>
        /// <returns></returns>
        int GetCurrentlyHoldLocks(int milliseconds = 0);

        bool CheckReadLockIsActive(Guid sessionId);
        void CloseSession(Guid sessionId);
    }
}