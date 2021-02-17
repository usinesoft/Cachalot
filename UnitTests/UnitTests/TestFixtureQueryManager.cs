using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using Client.Core;
using Client.Messages;
using Client.Queries;
using NUnit.Framework;
using Server;
using Server.Queries;
using Tests.TestData;

namespace Tests.UnitTests
{
    [TestFixture]
    public class TestFixtureQueryManager
    {
        private static List<Order> GenerateOrders(int count)
        {
            var result = new List<Order>();

            var categories = new[] {"geek", "camping", "sf", "food", "games"};

            var rg = new Random(Environment.TickCount);

            var date = new DateTimeOffset(2020, 01, 04, 0, 0, 0, TimeSpan.Zero);

            for (var i = 0; i < count; i++)
            {
                result.Add(new Order
                {
                    Id = Guid.NewGuid(),
                    Amount = rg.NextDouble() * 100,
                    Category = categories[i % categories.Length],
                    ClientId = i % 100,
                    Date = date,
                    IsDelivered = i % 2 == 0,
                    ProductId = rg.Next(10, 100),
                    Quantity = rg.Next(1, 5)
                });

                date = date.AddHours(1);
            }


            return result;
        }

        private static List<AllKindsOfProperties> GenerateAllKinds(int count)
        {
            var result = new List<AllKindsOfProperties>();

            var tags1 = new[] {"geek", "camping", "sf", "food", "games"};
            var tags2 = new[] {"space", "electronics", "sf"};
            var instruments = new[] {"flute", "piano", "guitar"};


            for (var i = 0; i < count; i++)
                result.Add(new AllKindsOfProperties
                {
                    Id = i + 1,
                    AreYouSure = (AllKindsOfProperties.Fuzzy) (i % 3),
                    Quantity = i % 10,
                    InstrumentName = instruments[i % 3],
                    Tags = new List<string>(i % 2 == 0 ? tags1 : tags2)
                });


            return result;
        }

        private static IEnumerable<Expression<Func<Order, bool>>> WhereClausesForOrders()
        {
            yield return o => o.Category == "geek" && !o.IsDelivered;

            yield return o => o.Category == "sf" && o.Amount > 10;

            yield return o => o.Category == "sf" && o.Amount > 15 && o.Amount < 20;

            yield return o => o.Category == "sf" && o.Amount >= 15 && o.Amount <= 20;

            yield return o => o.Id == Guid.Empty;

            var list = new[] {"sf", "games"};

            yield return o => o.Quantity >= 4;

            yield return o => list.Contains(o.Category);

            yield return o => o.Quantity >= 4 && list.Contains(o.Category);

            yield return o => o.Quantity >= 4 && !list.Contains(o.Category);

            yield return o => o.Quantity != 4 && list.Contains(o.Category);

            yield return o => o.Quantity != 4 && o.Category.StartsWith("g");
            
            yield return o => o.Quantity != 4 || o.Category.StartsWith("g");
        }

        private static IEnumerable<Expression<Func<AllKindsOfProperties, bool>>> WhereClausesForAllKinds()
        {
            yield return o => o.Quantity == 2 && o.Again == AllKindsOfProperties.Fuzzy.Maybe;

            yield return o => o.Quantity == 2 && o.Again > AllKindsOfProperties.Fuzzy.No;

            yield return o => o.Quantity >= 2 && o.Tags.Contains("sf");

            yield return o => o.Quantity >= 2 && o.Tags.Contains("camping");

            yield return o => o.Quantity >= 2 && !o.Tags.Contains("sf");
        }


        [Test]
        public void Test_functions_vs_queries()
        {
            var schema = TypedSchemaFactory.FromType<Order>();

            var queries = WhereClausesForOrders()
                .Select(w => ExpressionTreeHelper.PredicateToQuery(w, schema.CollectionName)).ToList();
            var predicates = WhereClausesForOrders().Select(w => w.Compile()).ToList();

            var count = queries.Count;

            Assert.AreEqual(count, predicates.Count);

            var objects = GenerateOrders(1000);


            var packed = objects.Select(o => PackedObject.Pack(o, schema)).ToList();


            var ds = new DataStore(schema, new NullEvictionPolicy(), new FullTextConfig());
            ds.InternalPutMany(packed, true);

            for (var i = 0; i < count; i++)
            {
                var fromObjects = objects.Where(predicates[i]).ToList();

                var qm = new QueryManager(ds);

                var fromDataSource = qm.ProcessQuery(queries[i]);


                Console.WriteLine($"{queries[i]} returned {fromDataSource.Count}");
                Console.WriteLine("execution plan:");
                Console.WriteLine(qm.ExecutionPlan);
                Console.WriteLine();


                Assert.AreEqual(fromObjects.Count, fromDataSource.Count);
            }
        }

        [Test]
        public void More_functions_vs_queries()
        {
            var schema = TypedSchemaFactory.FromType<AllKindsOfProperties>();

            var queries = WhereClausesForAllKinds()
                .Select(w => ExpressionTreeHelper.PredicateToQuery(w, schema.CollectionName)).ToList();
            var predicates = WhereClausesForAllKinds().Select(w => w.Compile()).ToList();

            var count = queries.Count;

            Assert.AreEqual(count, predicates.Count);

            var objects = GenerateAllKinds(1000);


            var packed = objects.Select(o => PackedObject.Pack(o, schema)).ToList();


            var ds = new DataStore(schema, new NullEvictionPolicy(), new FullTextConfig());
            ds.InternalPutMany(packed, true);

            for (var i = 0; i < count; i++)
            {
                var fromObjects = objects.Where(predicates[i]).ToList();

                var qm = new QueryManager(ds);

                var fromDataSource = qm.ProcessQuery(queries[i]);


                Console.WriteLine($"{queries[i]} returned {fromDataSource.Count}");
                Console.WriteLine("execution plan:");
                Console.WriteLine(qm.ExecutionPlan);
                Console.WriteLine();


                Assert.AreEqual(fromObjects.Count, fromDataSource.Count);
            }
        }

        [Test]
        public void Check_execution_plans()
        {
            var schema = TypedSchemaFactory.FromType<AllKindsOfProperties>();

            var objects = GenerateAllKinds(100_000);

            var packed = objects.Select(o => PackedObject.Pack(o, schema)).ToList();


            var ds = new DataStore(schema, new NullEvictionPolicy(), new FullTextConfig());
            ds.InternalPutMany(packed, true);


            var qm = new QueryManager(ds);

            // first time for warm-up
            qm.ProcessQuery(ExpressionTreeHelper.PredicateToQuery<AllKindsOfProperties>(a => a.Tags.Contains("food")));

            qm.ProcessQuery(ExpressionTreeHelper.PredicateToQuery<AllKindsOfProperties>(a => a.Tags.Contains("food")));

            Console.WriteLine(qm.ExecutionPlan);

            Assert.AreEqual(1, qm.ExecutionPlan.QueryPlans.Count);
            Assert.IsTrue(qm.ExecutionPlan.QueryPlans[0].SimpleQueryStrategy);
            Assert.IsFalse(qm.ExecutionPlan.QueryPlans[0].FullScan);

            // first time for warm-up
            qm.ProcessQuery(ExpressionTreeHelper.PredicateToQuery<AllKindsOfProperties>(a =>
                a.Tags.Contains("food") && a.Tags.Contains("space")));

            var result =
                qm.ProcessQuery(ExpressionTreeHelper.PredicateToQuery<AllKindsOfProperties>(a =>
                    a.Tags.Contains("food") && a.Tags.Contains("space")));
            Assert.AreEqual(0, result.Count);
            Assert.AreEqual(1, qm.ExecutionPlan.QueryPlans.Count);
            Assert.IsFalse(qm.ExecutionPlan.QueryPlans[0].SimpleQueryStrategy);
            Assert.IsFalse(qm.ExecutionPlan.QueryPlans[0].FullScan,
                "this query should be solved by an index not a full-scan");

            Console.WriteLine(qm.ExecutionPlan);

            // first time for warm-up
            qm.ProcessQuery(
                ExpressionTreeHelper.PredicateToQuery<AllKindsOfProperties>(a => a.Quantity > 1 && a.Quantity < 2));

            result = qm.ProcessQuery(
                ExpressionTreeHelper.PredicateToQuery<AllKindsOfProperties>(a => a.Quantity > 1 && a.Quantity < 2));
            Console.WriteLine(qm.ExecutionPlan);

            Assert.AreEqual(0, result.Count);
            Assert.AreEqual(1, qm.ExecutionPlan.QueryPlans.Count);
            Assert.IsTrue(qm.ExecutionPlan.QueryPlans[0].SimpleQueryStrategy,
                "this query should have been optimized as a range query and executed as a simple query");
            Assert.IsTrue(qm.ExecutionPlan.QueryPlans[0].FullScan,
                "this query should be executed as full-scan as the index is not ordered");

            // first time for warm-up
            qm.ProcessQuery(
                ExpressionTreeHelper.PredicateToQuery<AllKindsOfProperties>(a => a.Quantity >= 1 || a.Quantity <= 2));

            result = qm.ProcessQuery(
                ExpressionTreeHelper.PredicateToQuery<AllKindsOfProperties>(a => a.Quantity >= 1 || a.Quantity <= 2));
            Console.WriteLine(qm.ExecutionPlan);

            Assert.AreEqual(100_000, result.Count);
            Assert.AreEqual(2, qm.ExecutionPlan.QueryPlans.Count, "this query should have been decomposed in two queries");
            Assert.IsTrue(qm.ExecutionPlan.QueryPlans[0].SimpleQueryStrategy);
            

            // empty query. Should return everything
            result = qm.ProcessQuery(OrQuery.Empty<AllKindsOfProperties>());
            Assert.AreEqual(100_000, result.Count);
            Console.WriteLine(qm.ExecutionPlan);
        }


        [Test]
        public void Query_performance()
        {
            var schema = TypedSchemaFactory.FromType<Order>();

            var queries = WhereClausesForOrders()
                .Select(w => ExpressionTreeHelper.PredicateToQuery(w, schema.CollectionName)).ToList();

            var count = queries.Count;

            var objects = GenerateOrders(100_000);

            var packed = objects.Select(o => PackedObject.Pack(o, schema)).ToList();


            var ds = new DataStore(schema, new NullEvictionPolicy(), new FullTextConfig());
            ds.InternalPutMany(packed, true);


            var watch = new Stopwatch();

            for (var i = 0; i < count; i++)
            {
                var qm = new QueryManager(ds);


                const int iterations = 100;

                // warm up 
                var returned = qm.ProcessQuery(queries[i]).Count;

                // run
                watch.Restart();

                for (var j = 0; j < iterations; j++) returned = qm.ProcessQuery(queries[i]).Count;

                watch.Stop();


                Console.WriteLine($"{queries[i]} returned {returned} took {watch.ElapsedMilliseconds / iterations} ms");
                Console.WriteLine("execution plan:");
                Console.WriteLine(qm.ExecutionPlan);
                Console.WriteLine();
            }
        }
    }
}