using Client.ChannelInterface;
using Client.Core;
using Client.Queries;
using ProtoBuf;
using System;

namespace Client.Messages
{
    [ProtoContract]
    public class GetRequest : DataRequest, IHasSession
    {
        public GetRequest(OrQuery query, Guid sessionId = default)
            : base(DataAccessType.Read, query.CollectionName, sessionId)
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