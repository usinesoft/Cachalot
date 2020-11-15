using Client.ChannelInterface;
using ProtoBuf;

namespace Client.Messages.Pivot
{
    [ProtoContract]
    public class PivotResponse:Response
    {
        [ field: ProtoMember(1)] public PivotLevel Root { get; set; } = new PivotLevel();
        public override ResponseType ResponseType  => ResponseType.Data;
    }
}