using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Server.Parsing
{
    public static class Tokenizer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static CharClass GetCharClass(char ch)
        {
            if (char.IsWhiteSpace(ch)) return CharClass.Whitespace;

            if ("<>:=,()".Contains(ch))
                return CharClass.Symbol;

            return CharClass.LetterOrDigit;
        }

        public static IList<Token> TokenizeOneLine(string input)
        {
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
                            NormalizedText = text.ToLower(),
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
                    NormalizedText = text.ToLower(),
                    Text = text,
                    TokenType = previousType
                });
            }


            return result;
        }
    }
}