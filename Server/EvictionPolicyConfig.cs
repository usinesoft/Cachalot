using Client.Interface;

namespace Server
{
    /// <summary>
    ///   Configuration of the eviction policy applied to a <see cref="DataStore" />
    /// </summary>
    public class EvictionPolicyConfig
    {
        public EvictionPolicyConfig(EvictionType type, int lruMaxItems, int lruEvictionCount)
        {
            Type = type;
            LruMaxItems = lruMaxItems;
            LruEvictionCount = lruEvictionCount;
        }


        public EvictionPolicyConfig()
        {
        }

        public EvictionType Type { get; set; } = EvictionType.None;

        /// <summary>
        ///   LRU eviction is triggered when this limit is reached
        /// </summary>
        public int LruMaxItems { get; set; }

        /// <summary>
        ///   When eviction is done, number of items items to be removed
        /// </summary>
        public int LruEvictionCount { get; set; }
    }
}