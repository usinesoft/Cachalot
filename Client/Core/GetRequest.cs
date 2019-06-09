using Client.ChannelInterface;
using Client.Messages;
using Client.Queries;
using ProtoBuf;

namespace Client.Core
{
    [ProtoContract]
    public class GetRequest : DataRequest
    {
        public GetRequest(OrQuery query)
            : base(DataAccessType.Read, query.TypeName)
        {
            Query = query;
        }


        /// <summary>
        ///     Used only for deserialization
        /// </summary>
        public GetRequest() : base(DataAccessType.Read, string.Empty)
        {
        }

        public override RequestClass RequestClass => RequestClass.DataAccess;

        [field: ProtoMember(1)] public OrQuery Query { get; }
    }
}