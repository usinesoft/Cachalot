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
        [ProtoMember(5)] private readonly List<OrQuery> _conditions = new List<OrQuery>();

        [ProtoMember(4)] private bool _isSingleStage;

        [ProtoMember(2)] private readonly List<CachedObject> _itemsToDelete = new List<CachedObject>();

        [ProtoMember(1)] private readonly List<CachedObject> _itemsToPut = new List<CachedObject>();

        [ProtoMember(3)] private string _transactionId;

        /// <summary>
        ///     Used for protobuf serialization
        /// </summary>
        public TransactionRequest()
        {
        }

        public TransactionRequest(IList<CachedObject> itemsToPut, IList<OrQuery> conditions,
            IList<CachedObject> itemsToDelete = null)
        {
            _itemsToPut = new List<CachedObject>(itemsToPut);

            _conditions = new List<OrQuery>(conditions);

            if (itemsToDelete != null) _itemsToDelete = new List<CachedObject>(itemsToDelete);
        }

        public override RequestClass RequestClass => RequestClass.DataAccess;
        public override bool IsSimple => IsSingleStage;

        public List<CachedObject> ItemsToPut => _itemsToPut;


        public string TransactionId
        {
            get => _transactionId;
            set => _transactionId = value;
        }

        public bool IsSingleStage
        {
            get => _isSingleStage;
            set => _isSingleStage = value;
        }


        public List<CachedObject> ItemsToDelete => _itemsToDelete;

        public List<OrQuery> Conditions => _conditions;


        public static string GenerateId()
        {
            return Guid.NewGuid().ToString();
        }


        public IList<DataRequest> SplitByType()
        {
            var result = new List<DataRequest>();

            var groups = _itemsToPut.GroupBy(i => i.FullTypeName);
            foreach (var group in groups)
            {
                var request = new PutRequest(group.Key)
                {
                    Items = group.ToList()
                };
                result.Add(request);
            }

            groups = _itemsToDelete.GroupBy(i => i.FullTypeName);
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