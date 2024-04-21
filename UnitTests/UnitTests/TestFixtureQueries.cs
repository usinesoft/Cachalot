using System;
using System.Linq;
using Client.Core;
using Client.Messages;
using Client.Queries;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Tests.TestData;

namespace Tests.UnitTests
{
    [TestFixture]
    public class TestFixtureQueries
    {
        private AtomicQuery MakeQuery(CollectionSchema schema, string name, QueryOperator op, object value)
        {
            return new AtomicQuery(schema.KeyByName(name), new KeyValue(value), op);
        }

        private AtomicQuery MakeInQuery(CollectionSchema schema, string name, params object[] values)
        {
            return new AtomicQuery(schema.KeyByName(name), values.Select(v => new KeyValue(v)).ToList());
        }

        private AtomicQuery MakeNinQuery(CollectionSchema schema, string name, params object[] values)
        {
            return new AtomicQuery(schema.KeyByName(name), values.Select(v => new KeyValue(v)).ToList(),
                QueryOperator.NotIn);
        }

        [Test]
        public void Atomic_query_match()
        {
            var item = new AllKindsOfProperties
            {
                Id = 13,
                Quantity = 2,
                AreYouSure = AllKindsOfProperties.Fuzzy.Maybe,
                IsDeleted = false,
                AnotherDate = DateTimeOffset.Now,
                InstrumentName = "Swap",
                Languages = { "fr", "en" },
                LastUpdate = DateTime.Now,
                Nominal = 125.23
            };

            var schema = TypedSchemaFactory.FromType<AllKindsOfProperties>();

            var packed = PackedObject.Pack(item, schema);

            // scalar operators first
            var q1 = MakeQuery(schema, "Id", QueryOperator.Eq, 13);
            ClassicAssert.IsTrue(q1.Match(packed));

            q1 = MakeQuery(schema, "Id", QueryOperator.Eq, 14);
            ClassicAssert.IsFalse(q1.Match(packed));

            q1 = MakeQuery(schema, "Id", QueryOperator.Lt, 13);
            ClassicAssert.IsFalse(q1.Match(packed));

            q1 = MakeQuery(schema, "Id", QueryOperator.Le, 13);
            ClassicAssert.IsTrue(q1.Match(packed));

            q1 = MakeQuery(schema, "AreYouSure", QueryOperator.Eq, AllKindsOfProperties.Fuzzy.Maybe);
            ClassicAssert.IsTrue(q1.Match(packed));

            q1 = MakeQuery(schema, "AreYouSure", QueryOperator.NotEq, AllKindsOfProperties.Fuzzy.No);
            ClassicAssert.IsTrue(q1.Match(packed));

            q1 = MakeQuery(schema, "InstrumentName", QueryOperator.StrStartsWith, "Sw");
            ClassicAssert.IsTrue(q1.Match(packed));

            q1 = MakeQuery(schema, "InstrumentName", QueryOperator.StrContains, "Swap");
            ClassicAssert.IsTrue(q1.Match(packed));

            q1 = MakeQuery(schema, "InstrumentName", QueryOperator.StrEndsWith, "Swap");
            ClassicAssert.IsTrue(q1.Match(packed));

            q1 = MakeQuery(schema, "InstrumentName", QueryOperator.Eq, "Swap");
            ClassicAssert.IsTrue(q1.Match(packed));

            q1 = MakeQuery(schema, "IsDeleted", QueryOperator.Eq, false);
            ClassicAssert.IsTrue(q1.Match(packed));

            q1 = MakeQuery(schema, "IsDeleted", QueryOperator.Lt, true);
            ClassicAssert.IsTrue(q1.Match(packed));

            q1 = MakeQuery(schema, "Nominal", QueryOperator.Lt, 150);
            ClassicAssert.IsTrue(q1.Match(packed));


            // IN

            // vector operators on scalar field 
            q1 = MakeInQuery(schema, "InstrumentName", "Swap", "Flute");
            ClassicAssert.IsTrue(q1.Match(packed));

            q1 = MakeInQuery(schema, "InstrumentName", "Piano", "Flute");
            ClassicAssert.IsFalse(q1.Match(packed));


            // NOT IN
            q1 = MakeNinQuery(schema, "InstrumentName", "Swap", "Flute");
            ClassicAssert.IsFalse(q1.Match(packed));

            q1 = MakeNinQuery(schema, "InstrumentName", "Piano", "Flute");
            ClassicAssert.IsTrue(q1.Match(packed));
        }

        [Test]
        public void Atomic_query_subset()
        {
            var products1 = new[] { 101, 102, 103 };
            var products2 = new[] { 101, 102 };

            var q1 = ExpressionTreeHelper.PredicateToQuery<Order>(o =>
                o.IsDelivered && products1.Contains(o.ProductId));
            var q2 = ExpressionTreeHelper.PredicateToQuery<Order>(o =>
                o.IsDelivered && products2.Contains(o.ProductId));

            ClassicAssert.IsTrue(q1.IsSubsetOf(q1));

            ClassicAssert.IsTrue(q2.IsSubsetOf(q1));

            ClassicAssert.IsFalse(q1.IsSubsetOf(q2));

            var q3 = ExpressionTreeHelper.PredicateToQuery<Order>(o => o.Quantity <= 10);
            var q4 = ExpressionTreeHelper.PredicateToQuery<Order>(o => o.Quantity < 10);
            var q5 = ExpressionTreeHelper.PredicateToQuery<Order>(o => o.Quantity <= 9);

            ClassicAssert.IsTrue(q5.IsSubsetOf(q3));
            ClassicAssert.IsTrue(q4.IsSubsetOf(q3));
            ClassicAssert.IsFalse(q3.IsSubsetOf(q5));


            // should be optimized as a between query (GeLe operator)
            var q6 = ExpressionTreeHelper.PredicateToQuery<Order>(o => o.Quantity <= 10 && o.Quantity >= 2);
            ClassicAssert.AreEqual(1, q6.Elements.Count);
            ClassicAssert.AreEqual(1, q6.Elements[0].Elements.Count);
            ClassicAssert.AreEqual(QueryOperator.GeLe, q6.Elements[0].Elements[0].Operator);


            var q7 = ExpressionTreeHelper.PredicateToQuery<Order>(o => o.Quantity <= 4 && o.Quantity >= 2);
            ClassicAssert.IsTrue(q7.IsSubsetOf(q6));

            var q8 = ExpressionTreeHelper.PredicateToQuery<Order>(o => o.Quantity <= 11 && o.Quantity >= 2);
            ClassicAssert.IsFalse(q8.IsSubsetOf(q6));
        }

        [Test]
        public void Range_queries()
        {
            // should be optimized as a between query (GeLe operator)
            var query = ExpressionTreeHelper.PredicateToQuery<Order>(o => o.Quantity <= 10 && o.Quantity >= 2);
            ClassicAssert.AreEqual(1, query.Elements.Count);
            ClassicAssert.AreEqual(1, query.Elements[0].Elements.Count);
            ClassicAssert.AreEqual(QueryOperator.GeLe, query.Elements[0].Elements[0].Operator);
            ClassicAssert.IsTrue(query.IsValid);
            Console.WriteLine(query.ToString());

            // should be optimized ad GtLe
            query = ExpressionTreeHelper.PredicateToQuery<Order>(o => o.Quantity <= 10 && o.Quantity > 2);
            ClassicAssert.AreEqual(1, query.Elements.Count);
            ClassicAssert.AreEqual(1, query.Elements[0].Elements.Count);
            ClassicAssert.AreEqual(QueryOperator.GtLe, query.Elements[0].Elements[0].Operator);
            ClassicAssert.IsTrue(query.IsValid);
            Console.WriteLine(query.ToString());

            // should be optimized ad GeLt
            query = ExpressionTreeHelper.PredicateToQuery<Order>(o => o.Quantity < 10 && o.Quantity >= 2);
            ClassicAssert.AreEqual(1, query.Elements.Count);
            ClassicAssert.AreEqual(1, query.Elements[0].Elements.Count);
            ClassicAssert.AreEqual(QueryOperator.GeLt, query.Elements[0].Elements[0].Operator);
            ClassicAssert.IsTrue(query.IsValid);
            Console.WriteLine(query.ToString());

            // should be optimized ad GtLt
            query = ExpressionTreeHelper.PredicateToQuery<Order>(o => o.Quantity < 10 && o.Quantity > 2);
            ClassicAssert.AreEqual(1, query.Elements.Count);
            ClassicAssert.AreEqual(1, query.Elements[0].Elements.Count);
            ClassicAssert.AreEqual(QueryOperator.GtLt, query.Elements[0].Elements[0].Operator);
            ClassicAssert.IsTrue(query.IsValid);
            Console.WriteLine(query.ToString());
        }
    }
}