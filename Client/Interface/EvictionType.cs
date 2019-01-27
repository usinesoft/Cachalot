namespace Client.Interface
{
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
        ///     Custom eviction policy (using an external plugin)
        /// </summary>
        Custom
    }
}