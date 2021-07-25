using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Client.Parsing
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


        /// <summary>
        /// Process multi-work keywords: "order by" for example should be treated as a single token
        /// </summary>
        /// <param name="original"></param>
        /// <param name="separator"></param>
        /// <returns></returns>
        static IEnumerable<Token> JoinIfKeyword(this IList<Token> original, params string[] separator)
        {
            for (int i = 0; i < original.Count -1; i++)
            {
                var joined = original[i].NormalizedText + " " + original[i+1].NormalizedText;
                if (separator.Contains(joined))
                {
                    yield return new Token{NormalizedText = joined, Text = joined, TokenType = CharClass.LetterOrDigit};
                    i++;
                }
                else
                {
                    yield return original[i];
                }
            }

            // the last one can not be part of a multi-word keyword
            yield return original[^1];
        }

        public static IDictionary<string, IList<Token>> Split(this IList<Token> original, params string[] separator)
        {

            original = original.JoinIfKeyword(separator).ToList();

            var result = new Dictionary<string, IList<Token>>();

            var one = new List<Token>();

            var lastSeparator = "";

            foreach (var token in original)
            {
                var sep = separator.FirstOrDefault(s => token.NormalizedText == s);

                if (sep != null)
                {
                    
                    result.Add(lastSeparator, one);
                    one = new List<Token>();
                
                    lastSeparator = sep;
                }
                else
                {
                    one.Add(token);
                }
            }

            
            result.Add(lastSeparator, one);
            

            return result;
        }

        public static string Join(this IList<Token> original, int startingAt = 0)
        {
            var result = new StringBuilder();

            for (int i = startingAt; i < original.Count; i++)
            {
                result.Append(original[i].Text);
            }

            return result.ToString();
        }
    }
}