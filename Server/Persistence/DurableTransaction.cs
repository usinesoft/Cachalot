using ProtoBuf;

namespace Server.Persistence
{
    [ProtoContract]
    [ProtoInclude(500, typeof(PutDurableTransaction))]
    [ProtoInclude(600, typeof(DeleteDurableTransaction))]
    [ProtoInclude(700, typeof(MixedDurableTransaction))]
    public abstract class DurableTransaction
    {
        /// <summary>
        ///     Used by protobuf serialization
        /// </summary>
        // ReSharper disable once EmptyConstructor
        // ReSharper disable once PublicConstructorInAbstractClass
        public DurableTransaction()
        {
        }
    }
}