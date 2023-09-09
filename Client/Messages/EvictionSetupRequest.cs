using Client.Core;
using Client.Interface;
using ProtoBuf;

namespace Client.Messages;

[ProtoContract]
public class EvictionSetupRequest : DataRequest
{
    /// <summary>
    ///     For serialization only
    /// </summary>
    public EvictionSetupRequest() : base(DataAccessType.Write, string.Empty)
    {
    }

    /// <summary>
    ///     Create a new request for the specified type. The domain description will be empty
    /// </summary>
    public EvictionSetupRequest(string collectionName, EvictionType evictionType, int limit = 0, int itemsToEvict = 0,
                                int timeToLiveInMilliseconds = 0)
        : base(DataAccessType.Write, collectionName)
    {
        Type = evictionType;
        Limit = limit;
        ItemsToEvict = itemsToEvict;
        TimeToLiveInMilliseconds = timeToLiveInMilliseconds;
    }

    [ProtoMember(1)] public EvictionType Type { get; }


    /// <summary>
    ///     The number of cached object that triggers the eviction
    /// </summary>
    [ProtoMember(2)]
    public int Limit { get; }


    /// <summary>
    ///     The number of cached objects evicted at once
    /// </summary>
    [ProtoMember(3)]
    public int ItemsToEvict { get; }

    [ProtoMember(4)] public int TimeToLiveInMilliseconds { get; }
}