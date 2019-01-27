using System.Collections.Generic;

namespace Client.Tools
{
    public static class FullTextExtensions
    {
        public static void Merge(this Token @this, Token other)
        {
            @this.Text = @this.Text + other.Text;
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
                    if (token.Text.Length == 1 && usefulSymbols.Contains(token.Text[0]))
                        result.Add(token);
                    else if (token.Text.Length > 1) result.Add(token);
                }
                else if (token.TokenType == CharClass.Digit)
                {
                    result.Add(token);
                }

            return result;
        }
    }
}