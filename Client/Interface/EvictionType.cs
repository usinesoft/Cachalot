namespace Client.Interface;

/// <summary>
///     Eviction policy applied to a cacheable type
/// </summary>
public enum EvictionType
{
    /// <summary>
    ///     No automatic eviction for this type
    /// </summary>
    None,

    /// <summary>
    ///     LRU eviction policy
    /// </summary>
    LessRecentlyUsed,

    /// <summary>
    ///     A time limit is imposed for the cached items
    /// </summary>
    TimeToLive
}