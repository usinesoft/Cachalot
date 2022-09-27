using Client.Core;
using ProtoBuf;
using System.Collections.Generic;
using System.Linq;

namespace Server.Persistence
{
    /// <summary>
    ///     Transaction containing multiple items to update or insert
    /// </summary>
    [ProtoContract]
    public class PutDurableTransaction : DurableTransaction
    {
        /// <summary>
        ///     Used by protobuf serialization
        /// </summary>
        // ReSharper disable once EmptyConstructor
        // ReSharper disable once PublicConstructorInAbstractClass
        public PutDurableTransaction()
        {
        }

        [ProtoMember(10)] public IList<PackedObject> Items { get; set; } = new List<PackedObject>();

        public IList<PutDurableTransaction> Split(int maxPacketSize)
        {
            
            return Items.Chunk(maxPacketSize).Select(c => new PutDurableTransaction { Items = c }).ToList(); 

        }
    }
}