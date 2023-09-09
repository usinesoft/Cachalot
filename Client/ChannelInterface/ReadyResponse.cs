using ProtoBuf;

namespace Client.ChannelInterface;

[ProtoContract]
public class ReadyResponse : Response
{
    public override ResponseType ResponseType => ResponseType.Ready;
}