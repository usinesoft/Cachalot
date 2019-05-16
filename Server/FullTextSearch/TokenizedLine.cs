using System.Collections.Generic;
using System.Text;

namespace Server.FullTextSearch
{
    public class TokenizedLine
    {
        public  IList<string> Tokens { get; set; } = new List<string>();

        public override string ToString()
        {
            var builder = new StringBuilder();

            foreach (var token in Tokens)
            {
                builder.Append(token).Append(" ");
            }

            return builder.ToString().Trim();
        }
    }
}