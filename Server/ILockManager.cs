using System;

namespace Server;

/// <summary>
///     Manages complex locking for multiple resources
/// </summary>
public interface ILockManager
{
    /// <summary>
    ///     Acquire read lock and run <see cref="Action" /> then release the read lock.
    ///     At least a resource name must be specified
    /// </summary>
    /// <param name="action">action to be executed inside lock</param>
    /// <param name="resourceNames">resources to be locked</param>
    void DoWithReadLock(Action action, params string[] resourceNames);


    /// <summary>
    ///     Acquire write lock and run <see cref="Action" /> then release the write lock.
    ///     At least a resource name must be specified
    /// </summary>
    /// <param name="action">action to be executed inside lock</param>
    /// <param name="resourceNames">resources to be locked</param>
    void DoWithWriteLock(Action action, params string[] resourceNames);


    /// <summary>
    ///     Open a lock session for specified resources.
    /// </summary>
    /// <param name="sessionId"></param>
    /// <param name="writeAccess">false if read-only</param>
    /// <param name="resourceNames"></param>
    /// <returns></returns>
    void AcquireLock(Guid sessionId, bool writeAccess, params string[] resourceNames);


    /// <summary>
    ///     Check if a lock with specified characteristics is currently hold
    /// </summary>
    /// <param name="sessionId"></param>
    /// <param name="writeAccess"></param>
    /// <param name="resourceNames"></param>
    /// <returns></returns>
    bool CheckLock(Guid sessionId, bool writeAccess, params string[] resourceNames);

    /// <summary>
    ///     Close the session and release all locks
    /// </summary>
    /// <param name="sessionId"></param>
    void CloseSession(Guid sessionId);


    /// <summary>
    ///     Forcibly remove all locks that are currently hold for more than the specified timespan
    /// </summary>
    /// <returns></returns>
    int ForceRemoveAllLocks(int olderThanInMilliseconds);


    /// <summary>
    ///     Returns the number of currently hold locks that are older than the specified timespan (all if 0)
    /// </summary>
    /// <param name="milliseconds"></param>
    /// <returns></returns>
    int GetCurrentlyHoldLocks(int milliseconds = 0);


    bool TryAcquireReadLock(Guid sessionId, int delayInMilliseconds, params string[] resourceNames);
    bool TryAcquireWriteLock(Guid sessionId, int delayInMilliseconds, params string[] resourceNames);
}