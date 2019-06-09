using Client.ChannelInterface;
using Client.Core;
using Client.Queries;
using ProtoBuf;

namespace Client.Messages
{
    [ProtoContract]
    public class GetDescriptionRequest : DataRequest
    {
        /// <summary>
        ///     For serialization only
        /// </summary>
        public GetDescriptionRequest() : base(DataAccessType.Read, string.Empty)
        {
        }

        public GetDescriptionRequest(OrQuery query)
            : base(DataAccessType.Read, query.TypeName)
        {
            Query = query;
        }

        public override RequestClass RequestClass => RequestClass.DataAccess;

        [field: ProtoMember(1)] public OrQuery Query { get; }
    }
}