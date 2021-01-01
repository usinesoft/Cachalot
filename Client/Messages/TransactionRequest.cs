using System;
using System.Collections.Generic;
using System.Linq;
using Client.ChannelInterface;
using Client.Core;
using Client.Queries;
using ProtoBuf;

namespace Client.Messages
{
    [ProtoContract]
    public class TransactionRequest : Request
    {
        /// <summary>
        ///     Used for protobuf serialization
        /// </summary>
        public TransactionRequest()
        {
        }

        public TransactionRequest(IList<PackedObject> itemsToPut, IList<OrQuery> conditions,
            IList<PackedObject> itemsToDelete = null)
        {
            ItemsToPut = new List<PackedObject>(itemsToPut);

            Conditions = new List<OrQuery>(conditions);

            if (itemsToDelete != null) ItemsToDelete = new List<PackedObject>(itemsToDelete);
        }

        public override RequestClass RequestClass => RequestClass.DataAccess;
        public override bool IsSimple => IsSingleStage;

        [field: ProtoMember(1)] public List<PackedObject> ItemsToPut { get; } = new List<PackedObject>();


        [field: ProtoMember(3)] public Guid TransactionId { get; set; }

        [field: ProtoMember(4)] public bool IsSingleStage { get; set; }


        [field: ProtoMember(2)] public List<PackedObject> ItemsToDelete { get; } = new List<PackedObject>();

        [field: ProtoMember(5)] public List<OrQuery> Conditions { get; } = new List<OrQuery>();


        public IList<DataRequest> SplitByType()
        {
            var result = new List<DataRequest>();

            var groups = ItemsToPut.GroupBy(i => i.CollectionName);
            foreach (var group in groups)
            {
                var request = new PutRequest(group.Key)
                {
                    Items = group.ToList()
                };
                result.Add(request);
            }

            groups = ItemsToDelete.GroupBy(i => i.CollectionName);
            foreach (var group in groups)
            {
                var items = group.ToList();
                foreach (var item in items)
                {
                    var request = new RemoveRequest(group.Key, item.PrimaryKey);
                    result.Add(request);
                }
            }

            return result;
        }
    }
}