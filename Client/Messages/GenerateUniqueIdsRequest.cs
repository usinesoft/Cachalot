using Client.ChannelInterface;
using ProtoBuf;

namespace Client.Messages;

[ProtoContract]
public class GenerateUniqueIdsRequest : Request
{
    public GenerateUniqueIdsRequest()
    {
    }

    public GenerateUniqueIdsRequest(int count, string name, int shardIndex, int shardCount)
    {
        Count = count;
        Name = name;
        ShardIndex = shardIndex;
        ShardCount = shardCount;
    }

    public override RequestClass RequestClass => RequestClass.UniqueIdGeneration;

    [ProtoMember(1)] public int Count { get; set; }


    [ProtoMember(2)] public string Name { get; set; }

    [ProtoMember(3)] public int ShardIndex { get; set; }


    [ProtoMember(4)] public int ShardCount { get; set; }
}