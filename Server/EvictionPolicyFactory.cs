using System;
using Client.Interface;

namespace Server
{
    public static class EvictionPolicyFactory
    {
        public static EvictionPolicy CreateEvictionPolicy(EvictionPolicyConfig config)
        {
            if (config.Type == EvictionType.None)
                return new NullEvictionPolicy();
            else if (config.Type == EvictionType.LessRecentlyUsed)
            {
                LruEvictionPolicy evictionPolicy = new LruEvictionPolicy(config.LruMaxItems, config.LruEvictionCount);
                return evictionPolicy;
            }

            throw new NotImplementedException(string.Format("{0} eviction policy is not implemented ", config.Type));
        }
    }
}