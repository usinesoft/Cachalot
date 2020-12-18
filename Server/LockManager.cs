using System;
using System.Threading;
using Client.Tools;
using JetBrains.Annotations;

namespace Server
{
    public class LockManager:ILockManager
    {
        private const int DefaultWaitForLockInMilliseconds = 10;

        readonly ReaderWriterLockSlim _globalLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        readonly SafeDictionary<string, ReaderWriterLockSlim> _locksByCollection = new SafeDictionary<string, ReaderWriterLockSlim>(()=> new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion) );

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
        }

        private void RemoveReadLock(string collectionName = null)
        {
            var @lock = Lock(collectionName);
            @lock.ExitReadLock();
        }

        private void AcquireWriteLock(string collectionName = null)
        {
            var @lock = Lock(collectionName);
            SmartRetry(() => @lock.TryEnterWriteLock(DefaultWaitForLockInMilliseconds));
        }

        private void RemoveWriteLock(string collectionName = null)
        {
            var @lock = Lock(collectionName);
            @lock.ExitWriteLock();
        }

        private bool TryAcquireReadLock(int delayInMilliseconds, params string[] collectionNames)
        {
            bool result = true;

            foreach (var collectionName in collectionNames)
            {
                var @lock = Lock(collectionName);

                result = @lock.TryEnterReadLock(delayInMilliseconds);
                if(!result )
                    break;
            }


            return result;
        }

        private void RemoveReadLock(params string[] collectionNames)
        {
            foreach (var collectionName in collectionNames)
            {
                var @lock = Lock(collectionName);
                if(@lock.IsReadLockHeld)
                    @lock.ExitReadLock();
            }

        }

        private bool TryAcquireWriteLock(int delayInMilliseconds, params string[] collectionNames)
        {
            bool result = true;

            foreach (var collectionName in collectionNames)
            {
                var @lock = Lock(collectionName);

                result = @lock.TryEnterWriteLock(delayInMilliseconds);
                if(!result )
                    break;
            }


            return result;
        }

        private void RemoveWriteLock(params string[] collectionNames)
        {
            foreach (var collectionName in collectionNames)
            {
                var @lock = Lock(collectionName);
                if(@lock.IsWriteLockHeld)
                    @lock.ExitWriteLock();
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
                if (TryAcquireReadLock(delayInMilliseconds, resourceNames))
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

        
    }
}