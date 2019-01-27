using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Client.Tools
{
    public class Tokenizer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CharClass GetCharClass(char ch)
        {
            if (ch >= 'a' && ch <= 'z') return CharClass.Letter;

            if (ch >= 'A' && ch <= 'Z') return CharClass.Letter;

            if (ch >= '0' && ch <= '9') return CharClass.Digit;

            if (ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n') return CharClass.Whitespace;

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


        public IList<Token> TokenizeOneLine(string input)
        {
            input = RemoveDiacritics(input);
            input = RemoveDoubleChars(input);
            input = input.ToLower();

            var result = new List<Token>(1000);


            var previousSplitPosition = 0;
            var previousType = CharClass.Start;


            for (var i = 0; i < input.Length; i++)
            {
                var current = input[i];

                var currentType = GetCharClass(current);

                if (currentType != previousType)
                {
                    if (previousType != CharClass.Whitespace && previousType != CharClass.Start)
                    {
                        var text = input.Substring(previousSplitPosition, i - previousSplitPosition);
                        result.Add(new Token
                        {
                            Text = text,
                            TokenType = previousType
                        });
                    }

                    previousType = currentType;

                    previousSplitPosition = i;
                }
            }

            if (previousType != CharClass.Whitespace)
            {
                var text = input.Substring(previousSplitPosition, input.Length - previousSplitPosition);
                result.Add(new Token
                {
                    Text = text,
                    TokenType = previousType
                });
            }


            return result;
        }
    }
}