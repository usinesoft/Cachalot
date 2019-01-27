using System.Collections.Generic;
using Client.Core;
using ProtoBuf;

namespace Server.Persistence
{
    /// <summary>
    /// A transaction containing both Put and delete requests
    /// </summary>
    [ProtoContract]
    public class MixedTransaction : Transaction
    {

        [ProtoMember(10)]
        public IList<CachedObject> ItemsToPut { get; set; } = new List<CachedObject>();


        [ProtoMember(20)]
        public IList<CachedObject> ItemsToDelete { get; set; } = new List<CachedObject>();

        /// <summary>
        /// Used by protobuf serialization
        /// </summary>
        // ReSharper disable once EmptyConstructor
        // ReSharper disable once PublicConstructorInAbstractClass
        public MixedTransaction()
        {
        }
    }
}