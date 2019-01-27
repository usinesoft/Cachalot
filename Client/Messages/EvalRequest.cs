using Client.ChannelInterface;
using Client.Core;
using Client.Queries;
using ProtoBuf;

namespace Client.Messages
{
    [ProtoContract]
    public class EvalRequest : DataRequest
    {
        [ProtoMember(1)] private readonly OrQuery _query;

        public EvalRequest() : base(DataAccessType.Read, string.Empty)
        {
        }

        public EvalRequest(OrQuery query)
            : base(DataAccessType.Read, query.TypeName)
        {
            _query = query;
        }

        public override RequestClass RequestClass => RequestClass.DataAccess;

        public OrQuery Query => _query;
    }
}