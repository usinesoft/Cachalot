using Client.Core;
using Client.Queries;
using ProtoBuf;

namespace Client.Messages
{
    /// <summary>
    ///     Remove a subset specified by a query
    /// </summary>
    [ProtoContract]
    public class RemoveManyRequest : DataRequest
    {
        public RemoveManyRequest() : base(DataAccessType.Write, string.Empty)
        {
        }

        public RemoveManyRequest(OrQuery query)
            : base(DataAccessType.Write, query.TypeName)
        {
            Query = query;
        }

        [field: ProtoMember(1)] public OrQuery Query { get; }
    }
}