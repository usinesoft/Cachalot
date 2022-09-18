using Client;
using Client.Tools;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Server.FullTextSearch
{
    public static class Tokenizer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static CharClass GetCharClass(char ch)
        {
            if (char.IsLetter(ch)) return CharClass.Letter;

            if (char.IsDigit(ch)) return CharClass.Digit;

            if (char.IsWhiteSpace(ch)) return CharClass.Whitespace;

            return CharClass.Symbol;
        }

        private static string RemoveDiacritics(string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark) stringBuilder.Append(c);
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }

        private static string RemoveDoubleChars(string text)
        {
            var stringBuilder = new StringBuilder();

            for (var i = 0; i < text.Length; i++)
                if (i == 0 || text[i] != text[i - 1])
                    stringBuilder.Append(text[i]);


            return stringBuilder.ToString();
        }


        private static string Normalize(string original)
        {
            var after = RemoveDiacritics(original);
            after = after.ToLower();
            return RemoveDoubleChars(after);
        }

        public static IList<Token> TokenizeOneLine(string input)
        {
            var result = new List<Token>(1000);


            var previousSplitPosition = 0;
            var previousType = CharClass.Start;
            var previousCasing = Casing.None;

            for (var i = 0; i < input.Length; i++)
            {
                var current = input[i];

                var currentType = GetCharClass(current);

                var currentCasing = char.IsUpper(current) ? Casing.Upper : Casing.Lower;

                if (currentType != previousType || currentType == previousType &&
                    (currentType == CharClass.Letter) & (currentCasing == Casing.Upper) &&
                    previousCasing == Casing.Lower
                ) // the second condition is used to tokenize camelCase or PascalCase strings
                {
                    if (previousType != CharClass.Whitespace && previousType != CharClass.Start)
                    {
                        var text = input.Substring(previousSplitPosition, i - previousSplitPosition);
                        result.Add(new Token
                        {
                            NormalizedText = Normalize(text),
                            Text = text,
                            TokenType = previousType
                        });
                    }

                    previousType = currentType;

                    previousSplitPosition = i;
                }

                previousCasing = currentCasing;
            }

            if (previousType != CharClass.Whitespace)
            {
                var text = input.Substring(previousSplitPosition, input.Length - previousSplitPosition);
                result.Add(new Token
                {
                    NormalizedText = Normalize(text),
                    Text = text,
                    TokenType = previousType
                });
            }


            return result;
        }



        public static IList<TokenizedLine> Tokenize(IEnumerable<string> input)
        {
            var result = new List<TokenizedLine>();

            foreach (var line in input)
            {
                var one = TokenizeOneLine(line);
                result.Add(new TokenizedLine { Tokens = one.Select(t => t.NormalizedText).ToList() });
            }


            return result;
        }



        private enum Casing
        {
            Upper,
            Lower,
            None
        }
    }
}