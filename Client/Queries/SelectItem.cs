using ProtoBuf;

namespace Client.Queries;

/// <summary>
///     One column (property) from a select clause
/// </summary>
[ProtoContract]
public class SelectItem
{
    [ProtoMember(1)] public string Name { get; set; }
    [ProtoMember(2)] public string Alias { get; set; }
}