using Client.ChannelInterface;
using ProtoBuf;

namespace Client.Messages
{
    /// <summary>
    ///     Request a server-side type registration
    /// </summary>
    [ProtoContract]
    public class RegisterTypeRequest : Request
    {
        /// <summary>
        ///     For serialization only
        /// </summary>
        public RegisterTypeRequest()
        {
        }

        /// <summary>
        /// </summary>
        /// <param name="collectionSchema">The description of the type to be registered</param>
        /// <param name="shardIndex">Index of the node inside the cluster (0 based)</param>
        /// <param name="shardsInCluster">Nodes in cluster</param>
        /// <param name="collectionName"></param>
        public RegisterTypeRequest(CollectionSchema collectionSchema, int shardIndex = 0, int shardsInCluster = 1, string collectionName = null) 
        {
            CollectionSchema = collectionSchema;
            ShardIndex = shardIndex;
            ShardsInCluster = shardsInCluster;
            CollectionName = collectionName;
        }

        public override RequestClass RequestClass => RequestClass.Admin;

        /// <summary>
        ///     Get the description of the type to be registered
        /// </summary>
        [field: ProtoMember(1)]
        public CollectionSchema CollectionSchema { get; }

        [field: ProtoMember(2)] public int ShardIndex { get; }

        [field: ProtoMember(3)] public int ShardsInCluster { get; }

        [field: ProtoMember(4)] public string CollectionName { get; }
    }
}