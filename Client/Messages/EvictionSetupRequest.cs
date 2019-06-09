using Client.Core;
using Client.Interface;
using ProtoBuf;

namespace Client.Messages
{
    [ProtoContract]
    public class EvictionSetupRequest : DataRequest
    {
        /// <summary>
        ///     For serialization only
        /// </summary>
        public EvictionSetupRequest() : base(DataAccessType.Write, string.Empty)
        {
        }

        /// <summary>
        ///     Create a new request for the specified type. The domain description will be empty
        /// </summary>
        public EvictionSetupRequest(string fullTypeName, EvictionType evictionType, int limit = 0, int itemsToEvict = 0)
            : base(DataAccessType.Write, fullTypeName)
        {
            Type = evictionType;
            Limit = limit;
            ItemsToEvict = itemsToEvict;
        }

        [ProtoMember(1)] public EvictionType Type { get; }


        /// <summary>
        ///     The number of cached object that triggers the eviction
        /// </summary>
        [ProtoMember(2)]
        public int Limit { get; }


        /// <summary>
        ///     The number of cached objects evicted at once
        /// </summary>
        [ProtoMember(3)]
        public int ItemsToEvict { get; }
    }
}