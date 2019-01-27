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
        [ProtoMember(2)] private readonly int _shardIndex;
        [ProtoMember(3)] private readonly int _shardsInCluster;
        [ProtoMember(1)] private readonly TypeDescription _typeDescription;

        /// <summary>
        ///     For serialization only
        /// </summary>
        public RegisterTypeRequest()
        {
        }

        /// <summary>
        /// </summary>
        /// <param name="typeDescription">The description of the type to be registered</param>
        /// <param name="shardIndex">Index of the node inside the cluster (0 based)</param>
        /// <param name="shardsInCluster">Nodes in cluster</param>
        public RegisterTypeRequest(TypeDescription typeDescription, int shardIndex = 0, int shardsInCluster = 1)
        {
            _typeDescription = typeDescription;
            _shardIndex = shardIndex;
            _shardsInCluster = shardsInCluster;
        }

        public override RequestClass RequestClass => RequestClass.Admin;

        /// <summary>
        ///     Get the description of the type to be registered
        /// </summary>
        public TypeDescription TypeDescription => _typeDescription;

        public int ShardIndex => _shardIndex;

        public int ShardsInCluster => _shardsInCluster;
    }
}