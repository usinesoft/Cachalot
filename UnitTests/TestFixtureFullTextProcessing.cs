using System;
using System.Diagnostics;
using System.Linq;
using Client.Core;
using Client.Tools;
using NUnit.Framework;
using Server.FullTextSearch;
using UnitTests.TestData;
// ReSharper disable NotAccessedVariable

namespace UnitTests
{
    [TestFixture]
    public class TestFixtureFullTextProcessing
    {

        [Test]
        public void Tokenize_simple_text()
        {
            var tokenizer = new Tokenizer();

            var tokens = tokenizer.TokenizeOneLine("a simple test");

            Assert.AreEqual(3, tokens.Count);

            Assert.IsTrue(tokens.All(tk=> tk.TokenType == CharClass.Letter));

            // accents and double letters are ignored
            tokens = tokenizer.TokenizeOneLine("café commerce");
            Assert.AreEqual(2, tokens.Count);
            Assert.AreEqual("cafe", tokens[0].NormalizedText);
            Assert.AreEqual("comerce", tokens[1].NormalizedText);
            

        }



        [Test]
        public void Tokenize_if_casing_changed_inside_a_word()
        {
            var tokenizer = new Tokenizer();

            var tokens = tokenizer.TokenizeOneLine("camelCase");

            Assert.AreEqual(2, tokens.Count);
            Assert.AreEqual("camel", tokens[0].NormalizedText);
            Assert.AreEqual("case", tokens[1].NormalizedText);

            tokens = tokenizer.TokenizeOneLine("PascalCase");

            Assert.AreEqual(2, tokens.Count);
            Assert.AreEqual("pascal", tokens[0].NormalizedText);
            Assert.AreEqual("case", tokens[1].NormalizedText);

            tokens = tokenizer.TokenizeOneLine("some PascalCase and #camelCase");

            Assert.AreEqual(7, tokens.Count);
            
        }


        [Test]
        public void Symbols_processing()
        {
            var tokenizer = new Tokenizer();

            var tokens = tokenizer.TokenizeOneLine("on-line course").PostProcessSymbols();

            Assert.AreEqual(3, tokens.Count);

            Assert.IsTrue(tokens.All(tk => tk.TokenType == CharClass.Letter));

            tokens = tokenizer.TokenizeOneLine("A #tag (and some more) .").PostProcessSymbols();

            Assert.AreEqual(6, tokens.Count);
            
            Assert.AreEqual("a", tokens[0].NormalizedText);

            Assert.AreEqual("#", tokens[1].NormalizedText);

            Assert.AreEqual(CharClass.Symbol, tokens[1].TokenType);

            tokens = tokenizer.TokenizeOneLine("c++  age <= 10").PostProcessSymbols();

            Assert.AreEqual(5, tokens.Count);

            Assert.AreEqual("+", tokens[1].NormalizedText);

            Assert.AreEqual("<=", tokens[3].NormalizedText);

        }




        [Test]
        public void Pack_object_with_full_text_indexed_properties()
        {
            var description = ClientSideTypeDescription.RegisterType<Home>();

            Assert.AreEqual(4, description.FullTextIndexed.Count);
            var home = new Home
            {
                Address = "14 rue du chien qui fume", Bathrooms = 2, Rooms = 4, PriceInEuros = 200, CountryCode = "FR",
                Comments =
                {
                    new Comment{Text = "Wonderful place", User = "foo"},
                    new Comment{Text = "Very nice apartment"}
                }
            };

            var packed = CachedObject.Pack(home);

            Assert.AreEqual(5, packed.FullText.Length);
            Assert.IsTrue(packed.FullText.Any(t=>t.Contains("chien qui fume")));

            // now pack the same object as json
            var json = SerializationHelper.ObjectToJson(home);

            var packed2 = CachedObject.PackJson(json, description.AsTypeDescription);
            Assert.AreEqual(5, packed2.FullText.Length);
            Assert.IsTrue(packed2.FullText.Any(t => t.Contains("chien qui fume")));

        }


        [Test]
        public void Test_packing_performance()
        {
            var home = new Home
            {
                Address = "14 rue du chien qui fume",
                Bathrooms = 2,
                Rooms = 4,
                PriceInEuros = 200,
                CountryCode = "FR",
                Comments =
                {
                    new Comment{Text = "Wonderful place", User = "foo"},
                    new Comment{Text = "Very nice apartment"}
                }
            };


            var desc = ClientSideTypeDescription.RegisterType<Home>();
            const int objects = 10_000;

            {

                // warm up
                             
                var unused = CachedObject.Pack(home, desc);
                var json = unused.AsJson();
                var reloaded = CachedObject.Unpack<Home>(unused);


                var watch = new Stopwatch();
                
                watch.Start();

                for (int i = 0; i < objects; i++)
                {
                    var packed = CachedObject.Pack(home, desc);
                    reloaded = CachedObject.Unpack<Home>(unused);
                }

                watch.Stop();


                Console.WriteLine($"Packing + unpacking {objects} objects took {watch.ElapsedMilliseconds} ms");
            }

            
            {

                // warm up

                desc.UseCompression = true;

                var unused = CachedObject.Pack(home, desc);
                var reloaded = CachedObject.Unpack<Home>(unused);

                var watch = new Stopwatch();
                
                watch.Start();

                for (int i = 0; i < objects; i++)
                {
                    var packed = CachedObject.Pack(home, desc);
                    reloaded = CachedObject.Unpack<Home>(unused);
                }

                watch.Stop();


                Console.WriteLine($"Packing + unpacking {objects} objects with compression took {watch.ElapsedMilliseconds} ms");
            }



        }

    }
}