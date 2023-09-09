using Client.ChannelInterface;
using ProtoBuf;

namespace Client.Messages;

[ProtoContract]
public class StopRequest : Request
{
    /// <summary>
    ///     Mostly for protobuf serialization
    /// </summary>
    public StopRequest()
    {
    }

    public StopRequest(bool restart)
    {
        Restart = restart;
    }

    [ProtoMember(1)] public bool Restart { get; set; }


    public override RequestClass RequestClass => RequestClass.Admin;
}