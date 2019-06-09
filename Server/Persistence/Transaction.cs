using ProtoBuf;

namespace Server.Persistence
{
    [ProtoContract]
    [ProtoInclude(500, typeof(PutTransaction))]
    [ProtoInclude(600, typeof(DeleteTransaction))]
    [ProtoInclude(700, typeof(MixedTransaction))]
    public abstract class Transaction
    {
        /// <summary>
        ///     Used by protobuf serialization
        /// </summary>
        // ReSharper disable once EmptyConstructor
        // ReSharper disable once PublicConstructorInAbstractClass
        public Transaction()
        {
        }
    }
}