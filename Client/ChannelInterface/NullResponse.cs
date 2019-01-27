using ProtoBuf;

namespace Client.ChannelInterface
{
    /// <summary>
    ///     Void response. The request was successfully processed but no data is sent to the client
    /// </summary>
    [ProtoContract]
    public class NullResponse : Response
    {
        public override ResponseType ResponseType => ResponseType.Null;
    }
}