using Client.ChannelInterface;
using ProtoBuf;

namespace Client.Messages;

[ProtoContract]
public class GenerateUniqueIdsResponse : Response
{
    public GenerateUniqueIdsResponse()
    {
    }

    public GenerateUniqueIdsResponse(int[] ids)
    {
        Ids = ids;
    }

    public override ResponseType ResponseType => ResponseType.Data;


    [ProtoMember(1)] public int[] Ids { get; set; }
}