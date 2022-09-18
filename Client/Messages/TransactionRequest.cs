using Client.ChannelInterface;
using Client.Core;
using Client.Tools;
using JetBrains.Annotations;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Client.Messages
{
    [ProtoContract]
    public class TransactionRequest : Request
    {

        [field: ProtoMember(1)] public IList<DataRequest> ChildRequests { get; set; } = new List<DataRequest>();

        /// <summary>
        /// This property is assigned later when we find out if more than one node in the cluster participates into a transaction
        /// A single stage transaction is a transaction executed on a single node
        /// </summary>
        [field: ProtoMember(2)] public bool IsSingleStage { get; set; }

        [field: ProtoMember(3)] public Guid TransactionId { get; set; }

        /// <summary>
        ///     Used for protobuf serialization
        /// </summary>
        public TransactionRequest()
        {

        }

        public TransactionRequest(IList<DataRequest> requests)
        {
            ChildRequests = requests;
        }

        public override RequestClass RequestClass => RequestClass.DataAccess;

        public override bool IsSimple => IsSingleStage;


        /// <summary>
        /// Split a transaction request between multiple shards
        /// </summary>
        /// <param name="serverSelector">a functions that gives the shard index for a primary key</param>
        /// <param name="serverCount">the total number of shards</param>
        /// <returns></returns>
        public SafeDictionary<int, TransactionRequest> SplitByServer([NotNull] Func<KeyValue, int> serverSelector, int serverCount)
        {
            if (serverSelector == null) throw new ArgumentNullException(nameof(serverSelector));

            var result = new SafeDictionary<int, TransactionRequest>(() => new TransactionRequest { TransactionId = TransactionId });

            foreach (var childRequest in ChildRequests)
            {
                TransactionRequest transactionRequest;
                int shard = 0;

                switch (childRequest)
                {

                    // simple put requests contain ore or more items from the same collection and no condition
                    case PutRequest simplePutRequest when !simplePutRequest.HasCondition:

                        var byServer = simplePutRequest.SplitByServer(serverSelector);

                        foreach (var pair in byServer)
                        {
                            transactionRequest = result.GetOrCreate(pair.Key);
                            transactionRequest.ChildRequests.Add(pair.Value);
                        }

                        break;

                    // a put request with a condition. It can contain a single item
                    case PutRequest conditionalUpdate when conditionalUpdate.HasCondition:
                        shard = serverSelector(conditionalUpdate.Items.Single().PrimaryKey);
                        transactionRequest = result.GetOrCreate(shard);
                        transactionRequest.ChildRequests.Add(conditionalUpdate);
                        break;

                    // a simple delete request contains one object to be deleted
                    case RemoveRequest simpleDeleteRequest:
                        shard = serverSelector(simpleDeleteRequest.PrimaryKey);
                        transactionRequest = result.GetOrCreate(shard);
                        transactionRequest.ChildRequests.Add(simpleDeleteRequest);
                        break;

                    // a delete many request contains a predicate that needs to be send to all servers
                    case RemoveManyRequest deleteMany:
                        for (int i = 0; i < serverCount; i++)
                        {
                            transactionRequest = result.GetOrCreate(i);
                            transactionRequest.ChildRequests.Add(deleteMany);
                        }
                        break;

                }
            }


            return result;
        }

        public string[] AllCollections
        {
            get
            {
                HashSet<string> result = new HashSet<string>();

                foreach (var childRequest in ChildRequests)
                {
                    result.Add(childRequest.CollectionName);
                }

                return result.ToArray();
            }
        }

        public PutRequest[] ConditionalRequests
        {
            get
            {
                var requestsWithConditions = ChildRequests
                    .Where(r => r is PutRequest pRequest &&
                                pRequest.HasCondition).Cast<PutRequest>().ToList();

                return requestsWithConditions.ToArray();
            }
        }

    }
}