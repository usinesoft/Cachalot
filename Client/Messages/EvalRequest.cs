using Client.ChannelInterface;
using Client.Core;
using Client.Queries;
using ProtoBuf;

namespace Client.Messages
{
    [ProtoContract]
    public class EvalRequest : DataRequest
    {
        public EvalRequest() : base(DataAccessType.Read, string.Empty)
        {
        }

        public EvalRequest(OrQuery query)
            : base(DataAccessType.Read, query.CollectionName)
        {
            Query = query;
        }

        public override RequestClass RequestClass => RequestClass.DataAccess;

        [field: ProtoMember(1)] public OrQuery Query { get; }
    }
}