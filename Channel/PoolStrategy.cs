//#define DEBUG_VERBOSE

#region

using Client;
using System;
using System.Collections.Generic;
using System.Threading;

#endregion

namespace Channel
{
    /// <summary>
    ///     Strategy pattern applied to an abstract resource pool.
    ///     The abstract part is implemented in this class, new resource retrieval,
    ///     resource validation, and resource disposal
    ///     is delegated to the concrete inheritor
    /// </summary>
    public abstract class PoolStrategy<T> : IDisposable where T : class
    {
        private const int DefaultMaxPendingClaims = 4;

        private readonly Queue<T> _pool;

        private long _maxPendingClaims = DefaultMaxPendingClaims;

        private long _pendingResourceClaims;

        private readonly SemaphoreSlim _poolEvent;


        protected PoolStrategy(int poolCapacity)
        {
            PoolCapacity = poolCapacity;
            _pool = new Queue<T>(poolCapacity);

            _poolEvent = new SemaphoreSlim(0, PoolCapacity);


        }

        public int PoolCapacity { get; }

        /// <summary>
        ///     Maximum simultaneous calls allowed to be made for a new resource
        ///     As an inheritor may dynamically change this parameter, access is synchronized
        /// </summary>
        public long MaxPendingClaims
        {
            get => Interlocked.Read(ref _maxPendingClaims);
            protected set => Interlocked.Exchange(ref _maxPendingClaims, value);
        }

        /// <summary>
        ///     Currently available resources in the pool
        /// </summary>
        public int ResourcesInPool
        {
            get
            {
                lock (_pool)
                {
                    return (int)_pool?.Count;
                }
            }
        }

        protected void PreLoad(int resourcesToPreLoad)
        {
            for (var i = 0; i < resourcesToPreLoad; i++)
            {
                var res = GetShinyNewResource();
                if (res != null)
                    InternalPut(res);
                else
                    break;
            }
        }

        /// <summary>
        ///     Put a newly released or a newly created resource into the the pool. If more resources are available
        ///     than the pool capacity still add the new one as it is supposed to be fresher than the ones in the pool,
        ///     and remove old ones
        /// </summary>
        /// <param name="resource"></param>
        private void InternalPut(T resource)
        {
            lock (_pool)
            {
                //Dbg.Trace($"pool:{string.Join(' ',_pool.Select(p=> p != null?"1":"0"))} in Put");

                Dbg.Trace($"pool: current {_pool.Count} max {PoolCapacity} semaphore {_poolEvent.CurrentCount} pending claims {_pendingResourceClaims} out of {MaxPendingClaims} in InternalPut before");

                _pool.Enqueue(resource);


                if (_pool.Count > PoolCapacity)
                {
                    Dbg.Trace("Too many resources. Remove older ones");

                    while (_pool.Count > PoolCapacity)
                    {
                        var toDispose = _pool.Dequeue();
                        Release(toDispose);
                    }

                }

                if (_poolEvent.CurrentCount < _pool.Count)
                {
                    _poolEvent.Release(_pool.Count - _poolEvent.CurrentCount);
                }

                Dbg.Trace($"pool: current {_pool.Count} max {PoolCapacity} semaphore {_poolEvent.CurrentCount} pending claims {_pendingResourceClaims} out of {MaxPendingClaims} in InternalPut after");

            }
        }

        /// <summary>
        ///     Asynchronously try to retrieve a resource (none was available in the pool)
        /// </summary>
        private void AsyncClaimNewResource()
        {
            var pending = Interlocked.Read(ref _pendingResourceClaims);
            if (pending < MaxPendingClaims)
            {
                Interlocked.Increment(ref _pendingResourceClaims);

                ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        var newOne = GetShinyNewResource();

                        Dbg.Trace(newOne != null ? "new resource put into pool" : "can not get new resource");

                        InternalPut(newOne);
                        //assume the provider is down
                    }
                    catch (Exception e)
                    {
                        Dbg.Trace(e.ToString());
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _pendingResourceClaims);
                    }

                });
            }
        }

        /// <summary>
        ///     Get a resource from the pool. If none is available the caller is blocked until one becomes
        ///     available.
        /// </summary>
        /// <returns>
        ///     If null is returned then the pool is empty and the external resource provider is not
        ///     available any more
        /// </returns>
        public T Get()
        {

            bool poolIsEmpty = false;

            lock (_pool)
            {
                //Dbg.Trace($"pool:{string.Join(' ',_pool.Select(p=> p != null?"1":"0"))} in Get");
                Dbg.Trace($"pool:{GetHashCode()} resources {_pool.Count} max {PoolCapacity} semaphore {_poolEvent.CurrentCount} pending claims {_pendingResourceClaims} out of {MaxPendingClaims} in Get");


                if (_pool.Count == 0)
                {
                    AsyncClaimNewResource();
                    poolIsEmpty = true;

                }
                else
                {
                    Dbg.Trace($"pool:{GetHashCode()} resource found");
                    var resource = _pool.Dequeue();

                    //if null it means the external resource provider is not available anymore
                    if (resource == null)
                        return null;

                    //a pooled resource may not be valid any more
                    if (IsStillValid(resource))
                        return resource;
                }

            }

            // wait for new connections outside the pool lock
            if (poolIsEmpty)
            {
                _poolEvent.Wait();
            }


            return Get(); //recursive call


        }

        /// <summary>
        ///     Put a resource back into the pool
        /// </summary>
        /// <param name="resource"></param>
        public void Put(T resource)
        {
            if (resource == null)
                throw new ArgumentNullException(nameof(resource));

            InternalPut(resource);
        }

        /// <summary>
        ///     Release and remove al the pooled resources
        /// </summary>
        public void ClearAll()
        {
            lock (_pool)
            {
                foreach (var t in _pool)
                    Release(t);

                _pool.Clear();
            }
        }

        #region IDisposable Members

        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                lock (_pool)
                {
                    ClearAll();
                }

                _disposed = true;
                if (_poolEvent.CurrentCount < PoolCapacity)
                {
                    _poolEvent.Release(PoolCapacity - _poolEvent.CurrentCount);
                }

            }
        }

        #endregion

        #region abstract actions

        /// <summary>
        ///     Provides a new "fresh" resource from the outside world
        /// </summary>
        /// <returns></returns>
        protected abstract T GetShinyNewResource();

        /// <summary>
        ///     Check if a pooled resource is still valid
        /// </summary>
        /// <param name="resource"></param>
        /// <returns></returns>
        protected abstract bool IsStillValid(T resource);


        /// <summary>
        ///     This resource is no longer required by the pool so it can be released
        ///     Delegate resource disposal to concrete inheritor ( which will probably call dispose if the
        ///     resource is <see cref="IDisposable" /> but no need to assume it here)
        /// </summary>
        /// <param name="resource"></param>
        protected abstract void Release(T resource);

        #endregion
    }
}