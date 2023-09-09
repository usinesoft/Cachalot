using Client.ChannelInterface;
using ProtoBuf;

namespace Client.Messages;

/// <summary>
///     Dump all data into a directory
/// </summary>
[ProtoContract]
public class DumpRequest : Request
{
    [ProtoMember(1)] public string Path { get; set; }

    [ProtoMember(2)] public int ShardIndex { get; set; }

    public override RequestClass RequestClass => RequestClass.Admin;
}