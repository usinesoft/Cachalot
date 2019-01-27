using Client.ChannelInterface;
using Client.Core;
using Client.Queries;
using ProtoBuf;

namespace Client.Messages
{
    [ProtoContract]
    public class GetDescriptionRequest : DataRequest
    {
        [ProtoMember(1)] private readonly OrQuery _query;

        /// <summary>
        ///     For serialization only
        /// </summary>
        public GetDescriptionRequest() : base(DataAccessType.Read, string.Empty)
        {
        }

        public GetDescriptionRequest(OrQuery query)
            : base(DataAccessType.Read, query.TypeName)
        {
            _query = query;
        }

        public override RequestClass RequestClass => RequestClass.DataAccess;

        public OrQuery Query => _query;
    }
}