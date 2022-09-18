using Client;
using Client.Core;
using Client.Tools;
using NUnit.Framework;
using Server.FullTextSearch;
using System;
using System.Diagnostics;
using System.Linq;
using Tests.TestData;

// ReSharper disable NotAccessedVariable

namespace Tests.UnitTests
{
    [TestFixture]
    public class TestFixtureFullTextProcessing
    {

        static TokenizedLine Tokenize(string line)
        {
            return Tokenizer.Tokenize(new[] { line })[0];
        }

        [Test]
        public void Compute_ordered_multiplier()
        {
            var multiplier1 = FullTextIndex.ComputeBonusIfOrderIsPreserved(Tokenize("a b c"), Tokenize("b c a"));
            var multiplier2 = FullTextIndex.ComputeBonusIfOrderIsPreserved(Tokenize("a b c"), Tokenize("c b a"));

            Assert.IsTrue(multiplier1 > multiplier2);
            Assert.AreEqual(1, multiplier2); // no multiplier as the order is not preserved

            var multiplier3 = FullTextIndex.ComputeBonusIfOrderIsPreserved(Tokenize("nice evening"), Tokenize("nice evening"));
            var multiplier4 = FullTextIndex.ComputeBonusIfOrderIsPreserved(Tokenize("nice evening"), Tokenize("it was a nice evening"));

            Assert.AreEqual(multiplier3, multiplier4);
            Assert.IsTrue(multiplier3 > 1);

            var multiplier5 = FullTextIndex.ComputeBonusIfOrderIsPreserved(Tokenize("nice and happy evening"), Tokenize("nice evening"));
            var multiplier6 = FullTextIndex.ComputeBonusIfOrderIsPreserved(Tokenize("nice evening"), Tokenize("nice and happy evening"));

            Assert.IsTrue(multiplier6 > multiplier5);
            Assert.IsTrue(multiplier5 > 1);

            var multiplier7 =
                FullTextIndex.ComputeBonusIfOrderIsPreserved(Tokenize("nice view close beach"),
                    Tokenize("nice view and close to the beach"));
            Assert.IsTrue(multiplier7 > 10 * 3);

            var multiplier8 = FullTextIndex.ComputeBonusIfOrderIsPreserved(Tokenize("c++"), Tokenize("c++"));
            var multiplier9 = FullTextIndex.ComputeBonusIfOrderIsPreserved(Tokenize("c++"), Tokenize("+c"));

            Assert.IsTrue(multiplier8 > 1);
            Assert.AreEqual(1, multiplier9); // no multiplier as the order is not preserved
        }


        [Test]
        public void Pack_object_with_full_text_indexed_properties()
        {
            var description = TypedSchemaFactory.FromType<Home>();

            Assert.AreEqual(5, description.FullText.Count);
            var home = new Home
            {
                Address = "14 rue du chien qui fume",
                Bathrooms = 2,
                Rooms = 4,
                PriceInEuros = 200,
                CountryCode = "FR",
                Comments =
                {
                    new Comment {Text = "Wonderful place", User = "foo"},
                    new Comment {Text = "Very nice apartment"}
                },
                Contacts = { "mail", "phone" }
            };

            var packed = PackedObject.Pack(home, description);

            Assert.AreEqual(7, packed.FullText.Length);
            Assert.IsTrue(packed.FullText.Any(t => t.Contains("chien qui fume")));

            // now pack the same object as json
            var json = SerializationHelper.ObjectToJson(home);

            var packed2 = PackedObject.PackJson(json, description);
            Assert.AreEqual(7, packed2.FullText.Length);
            Assert.IsTrue(packed2.FullText.Any(t => t.Contains("chien qui fume")));
        }


        [Test]
        public void Symbols_processing()
        {


            var tokens = Tokenizer.TokenizeOneLine("on-line course").PostProcessSymbols();

            Assert.AreEqual(3, tokens.Count);

            Assert.IsTrue(tokens.All(tk => tk.TokenType == CharClass.Letter));

            tokens = Tokenizer.TokenizeOneLine("A #tag (and some more) .").PostProcessSymbols();

            Assert.AreEqual(6, tokens.Count);

            Assert.AreEqual("a", tokens[0].NormalizedText);

            Assert.AreEqual("#", tokens[1].NormalizedText);

            Assert.AreEqual(CharClass.Symbol, tokens[1].TokenType);

            tokens = Tokenizer.TokenizeOneLine("c++  age <= 10").PostProcessSymbols();

            Assert.AreEqual(5, tokens.Count);

            Assert.AreEqual("+", tokens[1].NormalizedText);

            Assert.AreEqual("<=", tokens[3].NormalizedText);
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
                    new Comment {Text = "Wonderful place", User = "foo"},
                    new Comment {Text = "Very nice apartment"}
                }
            };


            var desc = TypedSchemaFactory.FromType<Home>();
            const int objects = 10_000;

            {
                // warm up

                var unused = PackedObject.Pack(home, desc);
                var json = unused.AsJson(desc);
                var reloaded = PackedObject.Unpack<Home>(unused, desc);


                var watch = new Stopwatch();

                watch.Start();

                for (var i = 0; i < objects; i++)
                {
                    var packed = PackedObject.Pack(home, desc);
                    reloaded = PackedObject.Unpack<Home>(unused, desc);
                }

                watch.Stop();


                Console.WriteLine($"Packing + unpacking {objects} objects took {watch.ElapsedMilliseconds} ms");
            }


            {
                // warm up

                desc.StorageLayout = Layout.Compressed;

                var unused = PackedObject.Pack(home, desc);
                var reloaded = PackedObject.Unpack<Home>(unused, desc);

                var watch = new Stopwatch();

                watch.Start();

                for (var i = 0; i < objects; i++)
                {
                    var packed = PackedObject.Pack(home, desc);
                    reloaded = PackedObject.Unpack<Home>(unused, desc);
                }

                watch.Stop();


                Console.WriteLine(
                    $"Packing + unpacking {objects} objects with compression took {watch.ElapsedMilliseconds} ms");
            }
        }

        [Test]
        public void Compare_packing_result_for_different_methods()
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
                    new Comment {Text = "Wonderful place", User = "foo"},
                    new Comment {Text = "Very nice apartment"}
                }
            };


            var desc = TypedSchemaFactory.FromType<Home>();

            //// warm up
            //var unused = PackedObject.Pack(home, desc);
            //var v1 = unused.ToString();

            var unused = PackedObject.Pack(home, desc);
            var v2 = unused.ToString();

            var json = SerializationHelper.ObjectToJson(home);
            unused = PackedObject.PackJson(json, desc);
            var v3 = unused.ToString();

            //Assert.AreEqual(v1, v2);
            Assert.AreEqual(v2, v3);

        }


        [Test]
        public void Tokenize_if_casing_changed_inside_a_word()
        {


            var tokens = Tokenizer.TokenizeOneLine("camelCase");

            Assert.AreEqual(2, tokens.Count);
            Assert.AreEqual("camel", tokens[0].NormalizedText);
            Assert.AreEqual("case", tokens[1].NormalizedText);

            tokens = Tokenizer.TokenizeOneLine("PascalCase");

            Assert.AreEqual(2, tokens.Count);
            Assert.AreEqual("pascal", tokens[0].NormalizedText);
            Assert.AreEqual("case", tokens[1].NormalizedText);

            tokens = Tokenizer.TokenizeOneLine("some PascalCase and #camelCase");

            Assert.AreEqual(7, tokens.Count);
        }

        [Test]
        public void Tokenize_simple_text()
        {

            var tokens = Tokenizer.TokenizeOneLine("a simple test");

            Assert.AreEqual(3, tokens.Count);

            Assert.IsTrue(tokens.All(tk => tk.TokenType == CharClass.Letter));

            // accents and double letters are ignored
            tokens = Tokenizer.TokenizeOneLine("café commerce");
            Assert.AreEqual(2, tokens.Count);
            Assert.AreEqual("cafe", tokens[0].NormalizedText);
            Assert.AreEqual("comerce", tokens[1].NormalizedText);
        }
    }
}