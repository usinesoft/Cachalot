using Client.ChannelInterface;
using Client.Messages;
using Client.Queries;
using ProtoBuf;

namespace Client.Core
{
    [ProtoContract]
    public class GetRequest : DataRequest
    {
        [ProtoMember(1)] private readonly OrQuery _query;

        public GetRequest(OrQuery query)
            : base(DataAccessType.Read, query.TypeName)
        {
            _query = query;
        }


        /// <summary>
        ///     Used only for deserialization
        /// </summary>
        public GetRequest() : base(DataAccessType.Read, string.Empty)
        {
        }

        public override RequestClass RequestClass => RequestClass.DataAccess;

        public OrQuery Query => _query;

        [ProtoMember(2)] public bool OnlyIfComplete { get; set; }
    }
}