using Client.ChannelInterface;
using ProtoBuf;

namespace Client.Messages;

/// <summary>
///     Delete amm data from database
/// </summary>
[ProtoContract]
public class DropRequest : Request
{
    public override RequestClass RequestClass => RequestClass.Admin;

    [ProtoMember(1)] public bool Backup { get; set; }
}