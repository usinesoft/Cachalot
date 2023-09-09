using System.Collections.Generic;
using ProtoBuf;

namespace Server.Persistence;

/// <summary>
///     Transaction containing the primary keys of the items to delete
/// </summary>
[ProtoContract]
public class DeleteDurableTransaction : DurableTransaction
{
    /// <summary>
    ///     Used by protobuf serialization
    /// </summary>
    // ReSharper disable once EmptyConstructor
    // ReSharper disable once PublicConstructorInAbstractClass
    public DeleteDurableTransaction()
    {
    }

    /// <summary>
    ///     Primary keys of items to delete
    /// </summary>
    [ProtoMember(20)]
    public IList<string> GlobalKeysToDelete { get; set; } = new List<string>();
}