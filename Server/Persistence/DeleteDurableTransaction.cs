using Client.Core;
using ProtoBuf;
using System.Collections.Generic;

namespace Server.Persistence
{
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

        [ProtoMember(20)] public IList<PackedObject> ItemsToDelete { get; set; } = new List<PackedObject>();
    }
}