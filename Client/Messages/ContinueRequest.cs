using Client.ChannelInterface;
using ProtoBuf;

namespace Client.Messages
{
    /// <summary>
    ///     Exchanged during two stage transactions. Continue or abort current transaction 
    /// </summary>
    [ProtoContract]
    public class ContinueRequest : Request
    {
        public override RequestClass RequestClass => RequestClass.Continue;

        [ProtoMember(1)] public bool Rollback { get; set; }
    }
}