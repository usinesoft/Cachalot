

#region

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Client;

#endregion

namespace Channel;

/// <summary>
///     Strategy pattern applied to an abstract resource pool.
///     The abstract part is implemented in this class, new resource retrieval,
///     resource validation, and resource disposal
///     is delegated to the concrete inheritor
/// </summary>
public abstract class PoolStrategy<T> : IDisposable where T : class
{
    private const int DefaultMaxPendingClaims = 4;

    private readonly BlockingCollection<T> _blockingQueue = new();

    private long _maxPendingClaims = DefaultMaxPendingClaims;

    private long _pendingResourceClaims;

    private bool _disposed;

    readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();


    protected PoolStrategy(int poolCapacity)
    {
        if (poolCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(poolCapacity));
        PoolCapacity = poolCapacity;
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
    public int ResourcesInPool => _blockingQueue.Count;

    
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

        if (_disposed)
        {
            Release(resource);
            return;
        }

        _blockingQueue.Add(resource);

        while (_blockingQueue.Count > PoolCapacity)
        {
            var toRelease = _blockingQueue.Take();
            Release(toRelease);
        }

        
    }

    /// <summary>
    ///     Asynchronously try to retrieve a resource (none was available in the pool)
    /// </summary>
    private void AsyncClaimNewResource()
    {
        var pending = Interlocked.Read(ref _pendingResourceClaims);

        if (pending >= MaxPendingClaims) return;
        
        Interlocked.Increment(ref _pendingResourceClaims);

        Task.Run(() =>
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

        if (_blockingQueue.Count == 0)
        {
            AsyncClaimNewResource();
        }

        try
        {
            var resource =  _blockingQueue.Take(_tokenSource.Token);

            if (resource == null) // provider not available
            {
                return null;
            }
        
            if (IsStillValid(resource))
            {
                return resource;
            }

            // recursive call (in case a resource is not valid but the provider can produce new valid ones)
            return Get();
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        
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
        while (_blockingQueue.TryTake(out var toRelease))
        {
            Release(toRelease);
        }
        
        
    }

    #region IDisposable Members

    public void Dispose()
    {
        
        Dispose(true);

        
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if(_disposed)
            return;

        _tokenSource.Cancel();
        ClearAll();
        _blockingQueue.Dispose();

        _disposed = true;

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