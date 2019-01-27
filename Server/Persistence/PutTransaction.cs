using System.Collections.Generic;
using Client.Core;
using ProtoBuf;

namespace Server.Persistence
{
    /// <summary>
    /// Transaction containing multiple items to update or insert
    /// </summary>
    [ProtoContract]
    public class PutTransaction : Transaction
    {
        [ProtoMember(10)]
        public IList<CachedObject> Items { get; set; } = new List<CachedObject>();

        /// <summary>
        /// Used by protobuf serialization
        /// </summary>
        // ReSharper disable once EmptyConstructor
        // ReSharper disable once PublicConstructorInAbstractClass
        public PutTransaction()
        {
        }
    }
}