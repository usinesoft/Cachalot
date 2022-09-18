using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Client.Parsing
{
    public class Node
    {
        private string _errorMessage;
        public string Token { get; set; }

        public IList<Node> Children { get; } = new List<Node>();

        public string ErrorMessage
        {
            get { return _errorMessage ?? Children.FirstOrDefault(c => c.ErrorMessage != null)?.ErrorMessage; }
            set => _errorMessage = value;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            ToString(builder, 0);

            return builder.ToString();

        }

        void ToString(StringBuilder result, int level)
        {
            var indent = new string(' ', level * 4);
            result.Append(indent).AppendLine(Token);

            foreach (var child in Children)
            {
                child.ToString(result, level + 1);
            }
        }



    }

}