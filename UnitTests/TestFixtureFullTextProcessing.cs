using System.Linq;
using Client.Tools;
using NUnit.Framework;

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

    }
}