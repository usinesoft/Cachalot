using Client.Core;
using Client.Messages;
using Client.Queries;
using NUnit.Framework;
using Server;
using Server.Queries;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using Tests.TestData;

namespace Tests.UnitTests
{
    [TestFixture]
    public class TestFixtureQueryManager
    {


        private static List<AllKindsOfProperties> GenerateAllKinds(int count)
        {
            var result = new List<AllKindsOfProperties>();

            var tags1 = new[] { "geek", "camping", "sf", "food", "games" };
            var tags2 = new[] { "space", "electronics", "sf" };
            var instruments = new[] { "flute", "piano", "guitar" };


            for (var i = 0; i < count; i++)
                result.Add(new AllKindsOfProperties
                {
                    Id = i + 1,
                    AreYouSure = (AllKindsOfProperties.Fuzzy)(i % 3),
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

            var list = new[] { "sf", "games" };

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

            var objects = Order.GenerateTestData(1000);


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

            var objects = Order.GenerateTestData(100_000);

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

        [Test]
        public void Order_by()
        {
            var objects = Order.GenerateTestData(100_000);

            var schema = TypedSchemaFactory.FromType<Order>();

            var packed = objects.Select(o => PackedObject.Pack(o, schema)).ToList();


            var ds = new DataStore(schema, new NullEvictionPolicy(), new FullTextConfig());
            ds.InternalPutMany(packed, true);


            // empty query
            {
                var q = new OrQuery(schema.CollectionName) { OrderByProperty = "Amount" };

                var qm = new QueryManager(ds);

                var result = qm.ProcessQuery(q).Select(x => PackedObject.Unpack<Order>(x, schema)).ToList();
                Console.WriteLine(qm.ExecutionPlan.ToString());

                Assert.AreEqual(objects.Count, result.Count);

                // check sorted ascending
                for (int i = 0; i < result.Count - 1; i++)
                {
                    Assert.LessOrEqual((int)result[i].Amount * 10000, (int)result[i + 1].Amount * 10000);
                }


                q.OrderByIsDescending = true;

                result = qm.ProcessQuery(q).Select(x => PackedObject.Unpack<Order>(x, schema)).ToList();
                Console.WriteLine(qm.ExecutionPlan.ToString());
                Assert.AreEqual(objects.Count, result.Count);

                // check sorted descending
                for (int i = 0; i < result.Count - 1; i++)
                {
                    Assert.GreaterOrEqual((int)result[i].Amount * 10000, (int)result[i + 1].Amount * 10000);
                }
            }

            // atomic query
            {
                var q = ExpressionTreeHelper.PredicateToQuery<Order>(o => o.IsDelivered == false);

                // result from linq2object to be compared with ordered query
                var raw = objects.Where(o => o.IsDelivered == false).ToList();

                q.OrderByProperty = "Amount";

                var qm = new QueryManager(ds);

                var result = qm.ProcessQuery(q).Select(x => PackedObject.Unpack<Order>(x, schema)).ToList();
                Console.WriteLine(qm.ExecutionPlan.ToString());

                Assert.AreEqual(raw.Count, result.Count);

                // check sorted ascending
                for (int i = 0; i < result.Count - 1; i++)
                {
                    Assert.LessOrEqual((int)result[i].Amount * 10000, (int)result[i + 1].Amount * 10000);
                }


                q.OrderByIsDescending = true;

                result = qm.ProcessQuery(q).Select(x => PackedObject.Unpack<Order>(x, schema)).ToList();
                Console.WriteLine(qm.ExecutionPlan.ToString());
                Assert.AreEqual(raw.Count, result.Count);

                // check sorted descending
                for (int i = 0; i < result.Count - 1; i++)
                {
                    Assert.GreaterOrEqual((int)result[i].Amount * 10000, (int)result[i + 1].Amount * 10000);
                }
            }

            // simple AND query
            {
                var q = ExpressionTreeHelper.PredicateToQuery<Order>(o => o.IsDelivered == false && o.Category == "geek");

                var raw = objects.Where(o => o.IsDelivered == false && o.Category == "geek").ToList();

                q.OrderByProperty = "Amount";

                var qm = new QueryManager(ds);

                var result = qm.ProcessQuery(q).Select(x => PackedObject.Unpack<Order>(x, schema)).ToList();
                Console.WriteLine(qm.ExecutionPlan.ToString());

                Assert.AreEqual(raw.Count, result.Count);

                for (int i = 0; i < result.Count - 1; i++)
                {
                    Assert.LessOrEqual((int)result[i].Amount * 10000, (int)result[i + 1].Amount * 10000);
                }


                q.OrderByIsDescending = true;

                result = qm.ProcessQuery(q).Select(x => PackedObject.Unpack<Order>(x, schema)).ToList();
                Console.WriteLine(qm.ExecutionPlan.ToString());

                Assert.AreEqual(raw.Count, result.Count);

                for (int i = 0; i < result.Count - 1; i++)
                {
                    Assert.GreaterOrEqual((int)result[i].Amount * 10000, (int)result[i + 1].Amount * 10000);
                }
            }

            // complex OR query
            {
                var q = ExpressionTreeHelper.PredicateToQuery<Order>(o => o.IsDelivered == false && o.Category == "geek" || o.Category == "sf");

                var raw = objects.Where(o => o.IsDelivered == false && o.Category == "geek" || o.Category == "sf").ToList();

                q.OrderByProperty = "Amount";

                var qm = new QueryManager(ds);

                var result = qm.ProcessQuery(q).Select(x => PackedObject.Unpack<Order>(x, schema)).ToList();

                Console.WriteLine(qm.ExecutionPlan.ToString());

                Assert.AreEqual(raw.Count, result.Count);

                for (int i = 0; i < result.Count - 1; i++)
                {
                    Assert.LessOrEqual((int)result[i].Amount * 10000, (int)result[i + 1].Amount * 10000);
                }


                q.OrderByIsDescending = true;

                result = qm.ProcessQuery(q).Select(x => PackedObject.Unpack<Order>(x, schema)).ToList();

                Console.WriteLine(qm.ExecutionPlan.ToString());

                Assert.AreEqual(raw.Count, result.Count);

                for (int i = 0; i < result.Count - 1; i++)
                {
                    Assert.GreaterOrEqual((int)result[i].Amount * 10000, (int)result[i + 1].Amount * 10000);
                }

                // check that TAKE operator is applied after ORDER BY
                q.Take = 1;
                var max = qm.ProcessQuery(q).Select(x => PackedObject.Unpack<Order>(x, schema)).Single();

                Assert.AreEqual(max.Amount, result[0].Amount);
            }


        }

        [Test]
        public void Distinct_operator()
        {

            var objects = Order.GenerateTestData(100_000);

            var schema = TypedSchemaFactory.FromType<Order>();

            var packed = objects.Select(o => PackedObject.Pack(o, schema)).ToList();


            var ds = new DataStore(schema, new NullEvictionPolicy(), new FullTextConfig());
            ds.InternalPutMany(packed, true);

            // empty query
            {

                // result from linq2object to be compared with query
                var raw = objects.Select(o => new { o.Category, o.ClientId }).Distinct().ToList();

                var q = new OrQuery(schema.CollectionName) { Distinct = true };
                q.SelectClause.Add(new SelectItem { Name = "Category", Alias = "Category" });
                q.SelectClause.Add(new SelectItem { Name = "ClientId", Alias = "ClientId" });

                var qm = new QueryManager(ds);


                var result = qm.ProcessQuery(q).Select(x => PackedObject.Unpack<Order>(x, schema)).ToList();


                Assert.AreEqual(raw.Count, result.Count);

            }

            // atomic query
            {

                // result from linq2object to be compared with query
                var raw = objects.Where(o => o.IsDelivered).Select(o => new { o.Category, o.ClientId }).Distinct().ToList();

                var q = ExpressionTreeHelper.PredicateToQuery<Order>(o => o.IsDelivered);
                q.Distinct = true;
                q.SelectClause.Add(new SelectItem { Name = "Category", Alias = "Category" });
                q.SelectClause.Add(new SelectItem { Name = "ClientId", Alias = "ClientId" });

                var qm = new QueryManager(ds);


                var result = qm.ProcessQuery(q).Select(x => PackedObject.Unpack<Order>(x, schema)).ToList();


                Assert.AreEqual(raw.Count, result.Count);

            }

            // simple and query
            {

                // result from linq2object to be compared with query
                var raw = objects.Where(o => o.IsDelivered && o.Amount < 100).Select(o => new { o.Category, o.ClientId }).Distinct().ToList();

                var q = ExpressionTreeHelper.PredicateToQuery<Order>(o => o.IsDelivered && o.Amount < 100);
                q.Distinct = true;
                q.SelectClause.Add(new SelectItem { Name = "Category", Alias = "Category" });
                q.SelectClause.Add(new SelectItem { Name = "ClientId", Alias = "ClientId" });

                var qm = new QueryManager(ds);


                var result = qm.ProcessQuery(q).Select(x => PackedObject.Unpack<Order>(x, schema)).ToList();


                Assert.AreEqual(raw.Count, result.Count);

            }

            // complex or query
            {

                // result from linq2object to be compared with query
                var raw = objects.Where(o => o.IsDelivered && o.Amount < 100 || o.Category == "sf" && o.Quantity > 1).Select(o => new { o.Category, o.ClientId }).Distinct().ToList();

                var q = ExpressionTreeHelper.PredicateToQuery<Order>(o => o.IsDelivered && o.Amount < 100 || o.Category == "sf" && o.Quantity > 1);
                q.Distinct = true;
                q.SelectClause.Add(new SelectItem { Name = "Category", Alias = "Category" });
                q.SelectClause.Add(new SelectItem { Name = "ClientId", Alias = "ClientId" });

                var qm = new QueryManager(ds);


                var result = qm.ProcessQuery(q).Select(x => PackedObject.Unpack<Order>(x, schema)).ToList();


                Assert.AreEqual(raw.Count, result.Count);

                q.Take = 3;
                result = qm.ProcessQuery(q).Select(x => PackedObject.Unpack<Order>(x, schema)).ToList();
                Assert.AreEqual(3, result.Count);

            }
        }
    }
}