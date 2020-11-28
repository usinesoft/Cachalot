using System;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Server.Parsing;

namespace UnitTests
{
    [TestFixture]
    public class TestFixtureSql
    {

        [Test]
        public void Select_all_parsing()
        {
            var result = new Parser().ParseSql("select * from theTable");

            Assert.IsNull(result.ErrorMessage);

            Assert.AreEqual("select", result.Token);
            Assert.AreEqual(1, result.Children.Count);
            Assert.AreEqual("thetable", result.Children[0].Token);

            Console.WriteLine(result);

            // "*" and "from" are optional
            result = new Parser().ParseSql("select theTable");

            Assert.AreEqual("select", result.Token);
            Assert.AreEqual(1, result.Children.Count);
            Assert.AreEqual("thetable", result.Children[0].Token);
            
            Console.WriteLine(result);
            
            // should not work for now
            result = new Parser().ParseSql("select a, b from theTable");
            Assert.IsNotNull(result.ErrorMessage);

            
        }

        [Test]
        public void Parsing_with_single_expression()
        {
            var result = new Parser().ParseSql("select from persons where a== b");
            Assert.IsNull(result.ErrorMessage);

            Assert.AreEqual("select", result.Token);
            Assert.AreEqual(2, result.Children.Count);
            Assert.AreEqual("persons", result.Children[0].Token);
            Assert.AreEqual("where", result.Children[1].Token);

            Assert.IsNull(result.ErrorMessage);

            Console.WriteLine(result);
        }

        [Test]
        public void More_complex_parsing()
        {
            var result = new Parser().ParseSql("select from collection where a<>'ttt' or x < 1.22 and x >= 0,5 take 1 ");

            Assert.IsNull(result.ErrorMessage);

            Assert.AreEqual("select", result.Token);
            Assert.AreEqual(3, result.Children.Count);
           

            Console.WriteLine(result);

            result = new Parser().ParseSql("select from collection where client in (x, y , z) or category in 'geek', 'games' take 20");

         
            Assert.AreEqual("select", result.Token);
            Assert.AreEqual(3, result.Children.Count);
            
            Assert.IsTrue(result.Children.Any(c=>c.Token == "collection"));
            Assert.IsTrue(result.Children.Any(c=>c.Token == "take"));
            Assert.IsTrue(result.Children.Any(c=>c.Token == "where"));
            

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
            var result = new Parser().ParseSql("select from collection where client in (x, y , z) or category in 'geek', 'games'");
            result = new Parser().ParseSql("select from collection where a<>'ttt' or x < 1.22 and x >= 0,5  ");

            var watch = new Stopwatch();
            watch.Start();
            for (int i = 0; i < 1000; i++)
            {
                result = new Parser().ParseSql("select from collection where client in (x, y , z) or category in 'geek', 'games'");
            }

            watch.Stop();

            Console.WriteLine($"1000 call to parse took {watch.ElapsedMilliseconds} ms");
            
            watch.Restart();
            for (int i = 0; i < 1000; i++)
            {
                result = new Parser().ParseSql("select from collection where a<>'ttt' or x < 1.22 and x >= 0,5  ");
            }

            watch.Stop();

            Console.WriteLine($"1000 call to parse took {watch.ElapsedMilliseconds} ms");

        }
    }
}