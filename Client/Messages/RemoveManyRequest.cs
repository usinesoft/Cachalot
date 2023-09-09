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

        public RemoveManyRequest(OrQuery query, bool drop = false)
            : base(DataAccessType.Write, query.CollectionName)
        {
            Query = query;
            Drop = drop;
        }

        /// <summary>
        /// The query matching the items to delete. If empty delete all (truncate)
        /// </summary>
        [field: ProtoMember(1)] public OrQuery Query { get; }

        /// <summary>
        /// If TRUE Also remove schema information
        /// </summary>
        [field: ProtoMember(2)] public bool Drop { get; }
    }
}