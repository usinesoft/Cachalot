using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace Client;

[ProtoContract]
public class TokenizedLine
{
    [ProtoMember(1)] public IList<string> Tokens { get; set; } = new List<string>();

    public override string ToString()
    {
        var builder = new StringBuilder();

        foreach (var token in Tokens) builder.Append(token).Append(" ");

        return builder.ToString().Trim();
    }
}