using Client.Core;
using Client.Messages;
using Client.Parsing;
using Client.Queries;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using Tests.TestData;

namespace Tests.UnitTests
{
    [TestFixture]
    public class TestFixtureSql
    {

        static (string a, string op, string b) ExtractUnitaryQuery(Node root, int nthQuery = 0)
        {
            var where = root.Children.FirstOrDefault(x => x.Token == "where");
            Assert.IsNotNull(where);

            var expr = where.Children[0]?.Children[0]?.Children[nthQuery];
            Assert.IsNotNull(expr);
            Assert.AreEqual(2, expr.Children.Count); 
            

            return (expr.Children[0].Token, expr.Token, expr.Children[1].Token); 

        }

        [TestCase("like '%john%'", "like", "'%john%'")]
        [TestCase("a<>'%john%'", "a", "<>", "'%john%'")]
        [TestCase("a<> '%john%'", "a", "<>", "'%john%'")]
        [TestCase(" 'john' and jane", "'john'", "and", "jane")]
        [TestCase(" name = ''", "name", "=", "''")]
        [TestCase(" name = '' ", "name", "=", "''")]
        public void Tokenizer_test(string input, params string[] tokens)
        {
            var tks = Tokenizer.TokenizeOneLine(input);
            Assert.IsNotNull(tokens);
            Assert.AreEqual(tokens.Length, tks.Count);
            for (int i = 0; i < tokens.Length; i++)
            {
                Assert.AreEqual(tokens[i], tks[i].Text);
            }

        }

        [Test]
        public void Select_all_parsing()
        {
            var result = new Parser().ParseSql("select * from theTable");

            Assert.IsNull(result.ErrorMessage);

            Assert.AreEqual("select", result.Token);
            Assert.AreEqual(2, result.Children.Count);
            Assert.AreEqual("thetable", result.Children[1].Children[0].Token);

            Console.WriteLine(result);




        }

        [Test]
        [TestCase("select from persons where a== b", "a", "=", "b", 0)]
        [TestCase("select from persons where a = b", "a", "=", "b", 0)]
        [TestCase("select from persons where a = 'b'", "a", "=", "'b'", 0)]
        [TestCase("select from persons where a = 'b' and c < 15", "c", "<", "15", 1)]
        public void Parsing_with_single_expression(string expression, string a, string op, string b, int nth )
        {
            var result = new Parser().ParseSql(expression);
            Assert.IsNull(result.ErrorMessage);

            Assert.AreEqual("select", result.Token);
            Assert.AreEqual(3, result.Children.Count);
            Assert.AreEqual("projection", result.Children[0].Token);
            Assert.AreEqual("from", result.Children[1].Token);
            Assert.AreEqual("where", result.Children[2].Token);

            Assert.IsNull(result.ErrorMessage);

            var (va, vop, vb) = ExtractUnitaryQuery(result, nth);
            Assert.IsNotNull(va);
            Assert.IsNotNull(vb);
            Assert.IsNotNull(vop);
            Assert.AreEqual(a, va);
            Assert.AreEqual(b, vb);
            Assert.AreEqual(op, vop);

            Console.WriteLine(result);
        }


        

        [Test]
        public void Ignore_keywords_inside_strings()
        {
            var result = new Parser().ParseSql("select from persons where a== 'select b from c'");
            Assert.IsNull(result.ErrorMessage);

            Assert.AreEqual("select", result.Token);
            Assert.AreEqual(3, result.Children.Count);
            Assert.AreEqual("projection", result.Children[0].Token);
            Assert.AreEqual("from", result.Children[1].Token);
            Assert.AreEqual("where", result.Children[2].Token);

            Assert.IsNull(result.ErrorMessage);

            var (va, vop, vb) = ExtractUnitaryQuery(result);
            Assert.IsNotNull(va);
            Assert.IsNotNull(vb);
            Assert.IsNotNull(vop);

            Assert.AreEqual("a", va);
            Assert.AreEqual("'select b from c'", vb);
            Assert.AreEqual("=", vop);

            Console.WriteLine(result);
        }

        [Test]
        [TestCase("select from persons where a== 'rue d\\'Antin'")]
        public void Ignore_escaped_string_delimiters_inside_strings(string expression)
        {
            var result = new Parser().ParseSql(expression);
            Assert.IsNull(result.ErrorMessage);

            Assert.AreEqual("select", result.Token);
            Assert.AreEqual(3, result.Children.Count);
            Assert.AreEqual("projection", result.Children[0].Token);
            Assert.AreEqual("from", result.Children[1].Token);
            Assert.AreEqual("where", result.Children[2].Token);

            Assert.IsNull(result.ErrorMessage);

            var (va, vop, vb) = ExtractUnitaryQuery(result);
            Assert.IsNotNull(va);
            Assert.IsNotNull(vb);
            Assert.IsNotNull(vop);

            Assert.AreEqual("a", va);
            Assert.AreEqual("'rue d'Antin'", vb);
            Assert.AreEqual("=", vop);

            Console.WriteLine(result);
        }

        [Test]
        public void More_complex_parsing()
        {
            var result = new Parser().ParseSql("select from collection where a<>'ttt' or x < 1.22 and x >= 0,5 take 1 ");

            Assert.IsNull(result.ErrorMessage);

            Assert.AreEqual("select", result.Token);
            Assert.AreEqual(4, result.Children.Count);


            Console.WriteLine(result);

            result = new Parser().ParseSql("select from collection where client in (x, y , z) or category in 'geek', 'games' take 20");


            Assert.AreEqual("select", result.Token);
            Assert.AreEqual(4, result.Children.Count);

            Assert.IsTrue(result.Children.Any(c => c.Token == "from"));
            Assert.IsTrue(result.Children.Any(c => c.Token == "projection"));
            Assert.IsTrue(result.Children.Any(c => c.Token == "take"));
            Assert.IsTrue(result.Children.Any(c => c.Token == "where"));


            Assert.IsNull(result.ErrorMessage);

            Console.WriteLine(result);

            // check parsing errors
            result = new Parser().ParseSql("select from collection where a<>'ttt' or x < 1.22 and x + 1>= 0,5  ");

            Assert.IsNotNull(result.ErrorMessage);
        }

        [Test]
        public void Parsing_extension()
        {
            var result = new Parser().ParseSql("select from collection where a<>'ttt' or tags contains 'geek' and clients contains 156 take 10 ");


            Assert.IsNull(result.ErrorMessage);

            Console.WriteLine(result);
        }


        [Test]
        public void Parsing_with_not()
        {
            var result = new Parser().ParseSql("select from collection where a not in ('ttt', 'xxx') or tags not contains 'geek' and clients not contains 156  ");

            Assert.IsNull(result.ErrorMessage);

            Console.WriteLine(result);

        }

        [Test]
        public void Parsing_performance_test()
        {
            // warm up
            var _ = new Parser().ParseSql("select from collection where client in (x, y , z) or category in 'geek', 'games'");
            _ = new Parser().ParseSql("select from collection where a<>'ttt' or x < 1.22 and x >= 0,5  ");

            var watch = new Stopwatch();
            watch.Start();
            for (int i = 0; i < 1000; i++)
            {
                _ = new Parser().ParseSql("select from collection where client in (x, y , z) or category in 'geek', 'games'");
            }

            watch.Stop();

            Console.WriteLine($"1000 call to parse took {watch.ElapsedMilliseconds} ms");

            watch.Restart();
            for (int i = 0; i < 1000; i++)
            {
                _ = new Parser().ParseSql("select from collection where a<>'ttt' or x < 1.22 and x >= 0,5  ");
            }

            watch.Stop();

            Console.WriteLine($"1000 call to parse took {watch.ElapsedMilliseconds} ms");

        }

        [Test]
        public void Smart_parse_values()
        {
            object vi = JExtensions.SmartParse("123");

            Assert.IsTrue(vi is int);

            object vf = JExtensions.SmartParse("123,1");

            Assert.IsTrue(vf is double);

            vf = JExtensions.SmartParse("123.1");

            Assert.IsTrue(vf is double);

            var vd = JExtensions.SmartParse("2012-05-01");

            Assert.IsTrue(vd is DateTime);

            vd = JExtensions.SmartParse("01/05/2012");

            Assert.IsTrue(vd is DateTime);

            // looks like a data but it is not correct so it will be parsed like a string
            vd = JExtensions.SmartParse("45/15/2012");

            Assert.IsTrue(vd is string);

            var vb = JExtensions.SmartParse("true");
            Assert.IsTrue(vb is bool);

            vb = JExtensions.SmartParse("false");
            Assert.IsTrue(vb is bool);

            Assert.IsNull(JExtensions.SmartParse("null"));

        }

        static AtomicQuery FindAtomicQuery(OrQuery query, string property)
        {
            return query.Elements.SelectMany(e => e.Elements).FirstOrDefault(e => e.PropertyName == property);
        }

        [Test]
        public void Select_to_query()
        {

            var schema = SchemaFactory.New("collection").PrimaryKey("id")
                .WithServerSideValue("a")
                .WithServerSideValue("x", IndexType.Ordered)
                .WithServerSideValue("age", IndexType.Ordered)
                .WithServerSideValue("date", IndexType.Ordered)
                .Build();

            var result = new Parser().ParseSql("select from collection where a<>'ttt' or x < 1.22 and x >= 0,5 or age > 18 ");

            var query = result.ToQuery(schema);

            Assert.AreEqual("collection", query.CollectionName);

            Assert.IsTrue(query.IsValid);

            var result1 = new Parser().ParseSql("select * from collection where a != 'ttt' or x < 1.22 and x >= 0,5 or age > 18 ");

            var query1 = result1.ToQuery(schema);

            // the two queries must be identical
            Assert.AreEqual(query.ToString(), query1.ToString());

            // check if the atomic queries have been correctly generated
            var q1 = FindAtomicQuery(query, "a");
            Assert.NotNull(q1);
            Assert.AreEqual(KeyValue.OriginalType.String, q1.Value.Type);
            Assert.AreEqual(QueryOperator.NotEq, q1.Operator);

            var q2 = FindAtomicQuery(query, "x");
            Assert.NotNull(q2);
            Assert.AreEqual(KeyValue.OriginalType.SomeFloat, q2.Value.Type);
            Assert.AreEqual(QueryOperator.GeLt, q2.Operator, "query should be optimized as range operator");

            var q3 = FindAtomicQuery(query, "age");
            Assert.NotNull(q3);
            Assert.AreEqual(KeyValue.OriginalType.SomeInteger, q3.Value.Type);
            Assert.AreEqual(QueryOperator.Gt, q3.Operator);

            var query2 = new Parser().ParseSql("select * from collection where date = 2012-01-31 ").ToQuery(schema);
            var q4 = FindAtomicQuery(query2, "date");
            Assert.NotNull(q4);
            Assert.AreEqual(KeyValue.OriginalType.Date, q4.Value.Type);
            Assert.AreEqual(QueryOperator.Eq, q4.Operator);


        }

        [Test]
        public void Query_with_in_operator()
        {
            var schema = SchemaFactory.New("collection").PrimaryKey("id")
                .WithServerSideValue("a")
                .WithServerSideValue("x", IndexType.Ordered)
                .WithServerSideValue("age", IndexType.Ordered)
                .WithServerSideValue("date", IndexType.Ordered)
                .Build();

            {
                var query = new Parser().ParseSql("select from collection where a in (1, 2, 3)").ToQuery(schema);

                var q = FindAtomicQuery(query, "a");

                Assert.AreEqual(3, q.Values.Count);
                Assert.AreEqual(QueryOperator.In, q.Operator);
                Assert.IsTrue(q.Values.All(v => v.Type == KeyValue.OriginalType.SomeInteger));
            }

            {
                var query = new Parser().ParseSql("select from collection where a not  in (1, 2, 3)").ToQuery(schema);

                var q = FindAtomicQuery(query, "a");

                Assert.AreEqual(3, q.Values.Count);
                Assert.AreEqual(QueryOperator.NotIn, q.Operator);
                Assert.IsTrue(q.Values.All(v => v.Type == KeyValue.OriginalType.SomeInteger));
            }


        }

        [Test]
        public void Query_with_contains_operator()
        {
            var schema = SchemaFactory.New("items").PrimaryKey("id")
                .WithServerSideValue("tags")
                .Build();

            {
                var query = new Parser().ParseSql("select from items where tags contains 'geek' or tags contains electronics").ToQuery(schema);

                Assert.AreEqual("items", query.CollectionName);

                var q = FindAtomicQuery(query, "tags");

                Assert.AreEqual(QueryOperator.Contains, q.Operator);
                Assert.IsTrue(q.Values.All(v => v.Type == KeyValue.OriginalType.String));
            }

            {
                var query = new Parser().ParseSql("select * from items where tags not contains 'geek'").ToQuery(schema);

                var q = FindAtomicQuery(query, "tags");

                Assert.AreEqual(QueryOperator.NotContains, q.Operator);
                Assert.IsTrue(q.Values.All(v => v.Type == KeyValue.OriginalType.String));
            }


        }

        [Test]
        public void Query_with_string_operators()
        {
            var schema = SchemaFactory.New("items").PrimaryKey("id")
                .WithServerSideValue("name")
                .Build();

            {
                var query = new Parser().ParseSql("select from items where name like john% ").ToQuery(schema);

                var q = FindAtomicQuery(query, "name");

                Assert.AreEqual(QueryOperator.StrStartsWith, q.Operator);
                Assert.AreEqual("john", q.Value.StringValue);
                Assert.AreEqual(KeyValue.OriginalType.String, q.Value.Type);


            }

            {
                var query = new Parser().ParseSql("select from items where name like %john ").ToQuery(schema);

                var q = FindAtomicQuery(query, "name");

                Assert.AreEqual(QueryOperator.StrEndsWith, q.Operator);
                Assert.AreEqual("john", q.Value.StringValue);
                Assert.AreEqual(KeyValue.OriginalType.String, q.Value.Type);


            }

            {
                var query = new Parser().ParseSql("select * from items where name like '%john%' ").ToQuery(schema);

                var q = FindAtomicQuery(query, "name");

                Assert.AreEqual(QueryOperator.StrContains, q.Operator);
                Assert.AreEqual("john", q.Value.StringValue);
                Assert.AreEqual(KeyValue.OriginalType.String, q.Value.Type);


            }

        }

        [Test]
        public void Projection_query()
        {
            var schema = SchemaFactory.New("collection").PrimaryKey("id")
                .WithServerSideValue("a")
                .WithServerSideValue("fx", IndexType.Ordered)
                .WithServerSideValue("age", IndexType.Ordered)
                .WithServerSideValue("date", IndexType.Ordered)
                .Build();

            {
                var query = new Parser().ParseSql("select fx, age from collection where a in (1, 2, 3)").ToQuery(schema);

                var q = FindAtomicQuery(query, "a");

                Assert.AreEqual(3, q.Values.Count);
                Assert.AreEqual(QueryOperator.In, q.Operator);
                Assert.IsTrue(q.Values.All(v => v.Type == KeyValue.OriginalType.SomeInteger));

                Assert.AreEqual(2, query.SelectClause.Count);
                Assert.AreEqual("fx", query.SelectClause[0].Name);
                Assert.AreEqual("fx", query.SelectClause[0].Alias);

            }

            {
                // same with alias
                var query = new Parser().ParseSql("select fx forex, age from collection where a in (1, 2, 3)").ToQuery(schema);

                var q = FindAtomicQuery(query, "a");

                Assert.AreEqual(3, q.Values.Count);
                Assert.AreEqual(QueryOperator.In, q.Operator);
                Assert.IsTrue(q.Values.All(v => v.Type == KeyValue.OriginalType.SomeInteger));

                Assert.AreEqual(2, query.SelectClause.Count);
                Assert.AreEqual("fx", query.SelectClause[0].Name);
                Assert.AreEqual("forex", query.SelectClause[0].Alias);

            }

        }


        [Test]
        public void Other_query_operators()
        {
            var schema = SchemaFactory.New("collection").PrimaryKey("id")
                .WithServerSideValue("a")
                .WithServerSideValue("x", IndexType.Ordered)
                .WithServerSideValue("age", IndexType.Ordered)
                .WithServerSideValue("date", IndexType.Ordered)
                .Build();

            {
                var query = new Parser().ParseSql("select * from collection order by age take 10").ToQuery(schema);

                // no where clause
                Assert.AreEqual(0, query.Elements.Count);

                Assert.AreEqual(10, query.Take);
                Assert.AreEqual("age", query.OrderByProperty);
                Assert.IsFalse(query.OrderByIsDescending);

            }

            {
                var query = new Parser().ParseSql("select * from collection order by age descending").ToQuery(schema);

                // no where clause
                Assert.AreEqual(0, query.Elements.Count);

                Assert.AreEqual(0, query.Take); // no take clause
                Assert.AreEqual("age", query.OrderByProperty);
                Assert.IsTrue(query.OrderByIsDescending);

            }

            {
                var query = new Parser().ParseSql("select distinct a, x from collection").ToQuery(schema);

                // no where clause
                Assert.AreEqual(0, query.Elements.Count);

                Assert.AreEqual(0, query.Take); // no take clause

                Assert.AreEqual(2, query.SelectClause.Count);
                Assert.AreEqual("a", query.SelectClause[0].Name);
                Assert.AreEqual("x", query.SelectClause[1].Name);
                Assert.IsTrue(query.Distinct);

            }
        }


        [Test]
        public void Speed_test_sql_vs_linq()
        {
            var schema = TypedSchemaFactory.FromType<Order>();

            // warm-up 
            var categories = new string[] { "geek", "games" };

            var query1 = ExpressionTreeHelper.PredicateToQuery<Order>(
                o => o.IsDelivered && categories.Contains(o.Category) || o.Amount > 100 && o.Amount < 200,
                schema.CollectionName);

            var query2 = new Parser().ParseSql($"select * from {schema.CollectionName} where  isdelivered = true and category in (geek, games) or amount > 100 and amount < 200").ToQuery(schema);

            Assert.AreEqual(query1.ToString().ToLower(), query2.ToString().ToLower());



            const int iterations = 1000;

            {
                var clock = new Stopwatch();
                clock.Start();

                for (int i = 0; i < iterations; i++)
                {
                    query1 = ExpressionTreeHelper.PredicateToQuery<Order>(
                        o => o.IsDelivered && categories.Contains(o.Category) || o.Amount > 100 && o.Amount < 200,
                        schema.CollectionName);
                }

                clock.Stop();

                Console.WriteLine($"{iterations} iterations with linq took {clock.ElapsedMilliseconds} ms");
            }

            {
                var clock = new Stopwatch();
                clock.Start();

                for (int i = 0; i < iterations; i++)
                {
                    query2 = new Parser().ParseSql($"select * from {schema.CollectionName} where  isdelivered = true and category in (geek, games) or amount > 100 and amount < 200").ToQuery(schema);
                }

                clock.Stop();

                Console.WriteLine($"{iterations} iterations with sql took {clock.ElapsedMilliseconds} ms");
            }



        }

    }



}