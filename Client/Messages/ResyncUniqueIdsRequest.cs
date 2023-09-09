using System.Collections.Generic;
using Client.ChannelInterface;
using ProtoBuf;

namespace Client.Messages;

[ProtoContract]
public class ResyncUniqueIdsRequest : Request
{
    /// <summary>
    ///     For serialization only
    /// </summary>
    public ResyncUniqueIdsRequest()
    {
    }

    public ResyncUniqueIdsRequest(Dictionary<string, int> newValues, int shardIndex, int shardCount)
    {
        NewStartValues = newValues;

        ShardIndex = shardIndex;

        ShardCount = shardCount;
    }

    public override RequestClass RequestClass => RequestClass.UniqueIdGeneration;


    [ProtoMember(1)] public Dictionary<string, int> NewStartValues { get; set; }

    [ProtoMember(2)] public int ShardIndex { get; set; }


    [ProtoMember(3)] public int ShardCount { get; set; }
}