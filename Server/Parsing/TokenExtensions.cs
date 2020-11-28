using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Server.Parsing
{
    public static class TokenExtensions
    {
        public static bool Is(this Token token, string keyword)
        {
            if (token == null)
                return false;

            keyword = keyword.Trim().ToLower();

            if (keyword == token.Text)
                return true;


            if (keyword == token.NormalizedText)
                return true;

            return false;

        }

        public static bool IsOneOf(this Token token, params string[] keywords)
        {
            return keywords.Any(token.Is);
        }

        public static IList<Token> TrimLeft(this IList<Token> original, params string[] ignore)
        {
            return original.SkipWhile(tk => tk.IsOneOf(ignore)).ToList();
        }

        public static IList<Token> TrimRight(this IList<Token> original, params string[] ignore)
        {
            return original.Reverse().SkipWhile(tk => tk.IsOneOf(ignore)).Reverse().ToList();
        }

        public static IList<Token> Skip(this IList<Token> original, int toSkip)
        {
            return (original as IEnumerable<Token>).Skip(toSkip).ToList();
        }

        
        public static IList<IList<Token>> Split(this IList<Token> original, string separator)
        {
            var result = new List<IList<Token>>();

            var one = new List<Token>();

            foreach (var token in original)
            {
                if (token.NormalizedText == separator)
                {
                    if (one.Count > 0)
                    {
                        result.Add(one);
                        one = new List<Token>();
                    }
                }
                else
                {
                    one.Add(token);
                }
            }

            result.Add(one);

            return result;
        }

        public static string Join(this IList<Token> original, int startingAt = 0)
        {
            var result = new StringBuilder();

            for (int i = startingAt; i < original.Count; i++)
            {
                result.Append(original[i].NormalizedText);
            }

            return result.ToString();
        }
    }
}