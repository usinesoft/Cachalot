using System.Collections.Generic;
using Client.Core;
using ProtoBuf;

namespace Server.Persistence
{
    /// <summary>
    /// Transaction containing the primary keys of the items to delete
    /// </summary>
    [ProtoContract]
    public class DeleteTransaction : Transaction
    {
        [ProtoMember(20)]
        public IList<CachedObject> ItemsToDelete { get; set; } = new List<CachedObject>();

        /// <summary>
        /// Used by protobuf serialization
        /// </summary>
        // ReSharper disable once EmptyConstructor
        // ReSharper disable once PublicConstructorInAbstractClass
        public DeleteTransaction()
        {
        }
    }
}