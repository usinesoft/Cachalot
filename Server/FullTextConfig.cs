using System.Collections.Generic;

namespace Server;

public class FullTextConfig
{
    public int MaxIndexedTokens { get; set; } = 10_000_000;

    public int MaxTokensToIgnore { get; set; } = 100;

    public List<string> TokensToIgnore { get; set; } = new();
}