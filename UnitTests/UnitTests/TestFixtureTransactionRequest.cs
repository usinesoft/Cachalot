using Client.Core;
using Client.Messages;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Tests.TestData;

namespace Tests.UnitTests
{

    [TestFixture]
    public class TestFixtureTransactionRequest
    {

        [Test]
        public void Split_transaction_request()
        {
            var schema = TypedSchemaFactory.FromType<Order>();

            var requests = new List<DataRequest>();

            // simple put requests
            for (int i = 0; i < 100; i++)
            {
                var order1 = new Order { Id = Guid.NewGuid(), Category = "geek", ProductId = 123, Quantity = 1 };

                var putRequest = new PutRequest("orders");
                var packed1 = PackedObject.Pack(order1, schema, "orders");
                putRequest.Items.Add(packed1);

                requests.Add(putRequest);
            }

            // conditional put requests
            for (int i = 0; i < 100; i++)
            {
                var order1 = new Order { Id = Guid.NewGuid(), Category = "geek", ProductId = 123, Quantity = 1 };

                var putRequest = new PutRequest("orders");
                var packed1 = PackedObject.Pack(order1, schema, "orders");
                putRequest.Items.Add(packed1);
                putRequest.Predicate = ExpressionTreeHelper.PredicateToQuery<Order>(o => o.IsDelivered, "orders");

                requests.Add(putRequest);
            }

            // simple delete requests
            for (int i = 0; i < 100; i++)
            {
                var order1 = new Order { Id = Guid.NewGuid(), Category = "geek", ProductId = 123, Quantity = 1 };
                var packed1 = PackedObject.Pack(order1, schema, "orders1");
                var deleteRequest = new RemoveRequest("orders1", packed1.PrimaryKey);

                requests.Add(deleteRequest);
            }

            // delete many request
            var deleteMany = new RemoveManyRequest(ExpressionTreeHelper.PredicateToQuery<Order>(o => o.IsDelivered, "orders"));
            requests.Add(deleteMany);


            var transactionRequest = new TransactionRequest(requests) { TransactionId = Guid.NewGuid() };

            var split = transactionRequest.SplitByServer(k => k.GetHashCode() % 5, 5);

            var total = split.Values.Sum(r => r.ChildRequests.Count);

            // 300 uniformly distributed + 5 (delete many) cloned on each server
            Assert.AreEqual(305, total);

            Assert.IsTrue(split.Values.All(s => s.TransactionId == transactionRequest.TransactionId));

            var tr0 = split[0];

            Assert.IsTrue(tr0.ConditionalRequests.All(r => r.HasCondition));
            Assert.IsTrue(tr0.ConditionalRequests.Any());

            var deleteManyCount = tr0.ChildRequests.Count(r => r is RemoveManyRequest);
            Assert.AreEqual(1, deleteManyCount);

            Assert.AreEqual(2, tr0.AllCollections.Length);// orders and orders1

        }
    }
}
