using System.Collections.Generic;

namespace Client.Tools
{
    public static class FullTextExtensions
    {
        public static void Merge(this Token @this, Token other)
        {
            @this.NormalizedText = @this.NormalizedText + other.NormalizedText;
        }

        public static IList<Token> PostProcessSymbols(this IList<Token> tokens)
        {
            var usefulSymbols = new HashSet<char> {'+', '<', '>', '=', '#'};

            var result = new List<Token>(tokens.Count);

            foreach (var token in tokens)
                if (token.TokenType == CharClass.Letter)
                {
                    result.Add(token);
                }
                else if (token.TokenType == CharClass.Symbol)
                {
                    if (token.NormalizedText.Length == 1 && usefulSymbols.Contains(token.NormalizedText[0]))
                        result.Add(token);
                    else if (token.NormalizedText.Length > 1) result.Add(token);
                }
                else if (token.TokenType == CharClass.Digit)
                {
                    result.Add(token);
                }

            return result;
        }
    }
}