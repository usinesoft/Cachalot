using Client.ChannelInterface;
using ProtoBuf;

namespace Client.Messages;

[ProtoContract]
public class SwitchModeRequest : Request
{
    /// <summary>
    ///     Mostly for protobuf serialization
    /// </summary>
    public SwitchModeRequest()
    {
    }

    public SwitchModeRequest(int newMode)
    {
        NewMode = newMode;
    }

    /// <summary>
    ///     For now 0 = normal, 1 = read only
    /// </summary>
    [ProtoMember(2)]
    public int NewMode { get; set; }


    public override RequestClass RequestClass => RequestClass.Admin;
}