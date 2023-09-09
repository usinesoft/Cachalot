using Client.ChannelInterface;
using ProtoBuf;

namespace Client.Messages;

[ProtoContract]
public class LockResponse : Response
{
    public override ResponseType ResponseType => ResponseType.Ready;

    [ProtoMember(1)] public bool Success { get; set; }
}