namespace Client.Tools
{
    public class Token
    {
        public string Text { get; set; }

        public CharClass TokenType { get; set; }

        public override string ToString()
        {
            return $"{nameof(Text)}: {Text}, {nameof(TokenType)}: {TokenType}";
        }
    }
}