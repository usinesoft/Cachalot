namespace Client.Parsing;

public class Token
{
    public string NormalizedText { get; set; }
    public string Text { get; set; }

    public CharClass TokenType { get; set; }

    public override string ToString()
    {
        return $"{nameof(NormalizedText)}: {NormalizedText}, {nameof(TokenType)}: {TokenType}";
    }
}