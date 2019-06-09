using System;
using System.Collections.Generic;
using Client.Core;
using Client.Queries;
using ProtoBuf;

namespace Client.Messages
{
    [ProtoContract]
    public class GetAvailableRequest : DataRequest
    {
        [ProtoMember(1)] private readonly List<KeyValue> _keys = new List<KeyValue>();

        /// <summary>
        ///     Used only for protocol buffers serialization
        /// </summary>
        public GetAvailableRequest() : base(DataAccessType.Read, string.Empty)
        {
        }

        public GetAvailableRequest(Type itemType)
            : base(DataAccessType.Read, itemType.FullName)
        {
        }

        public IList<KeyValue> PrimaryKeys => _keys;

        [field: ProtoMember(2)] public Query MoreCriteria { get; set; }
    }
}