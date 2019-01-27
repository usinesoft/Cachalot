using Client.ChannelInterface;
using ProtoBuf;

namespace Client.Messages
{
    /// <summary>
    ///     Delete amm data from database
    /// </summary>
    [ProtoContract]
    public class ContinueRequest : Request
    {
        public override RequestClass RequestClass => RequestClass.Continue;

        [ProtoMember(1)] public bool Rollback { get; set; }
    }
}