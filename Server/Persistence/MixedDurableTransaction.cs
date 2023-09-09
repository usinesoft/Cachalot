using System.Collections.Generic;
using Client.Core;
using ProtoBuf;

namespace Server.Persistence;

/// <summary>
///     A transaction containing both Put and delete requests
/// </summary>
[ProtoContract]
public class MixedDurableTransaction : DurableTransaction
{
    /// <summary>
    ///     Used by protobuf serialization
    /// </summary>
    // ReSharper disable once EmptyConstructor
    // ReSharper disable once PublicConstructorInAbstractClass
    public MixedDurableTransaction()
    {
    }

    [ProtoMember(10)] public IList<PackedObject> ItemsToPut { get; set; } = new List<PackedObject>();


    [ProtoMember(20)] public IList<string> GlobalKeysToDelete { get; set; } = new List<string>();
}