using System.Collections.Generic;
using Client.Core;
using ProtoBuf;

namespace Server.Persistence
{
    /// <summary>
    ///     Transaction containing the primary keys of the items to delete
    /// </summary>
    [ProtoContract]
    public class DeleteTransaction : Transaction
    {
        /// <summary>
        ///     Used by protobuf serialization
        /// </summary>
        // ReSharper disable once EmptyConstructor
        // ReSharper disable once PublicConstructorInAbstractClass
        public DeleteTransaction()
        {
        }

        [ProtoMember(20)] public IList<PackedObject> ItemsToDelete { get; set; } = new List<PackedObject>();
    }
}