using Client.Messages;
using ProtoBuf;

namespace Client.ChannelInterface
{
    /// <summary>
    ///     Base abstract class for the responses (messsages sent from the server to the client)
    /// </summary>
    [ProtoContract]
    [ProtoInclude(500, typeof(EvalResponse))]
    [ProtoInclude(501, typeof(ExceptionResponse))]
    //[ProtoInclude(502, typeof (GetOneResponse))]
    [ProtoInclude(503, typeof(ItemsCountResponse))]
    [ProtoInclude(504, typeof(LogResponse))]
    [ProtoInclude(505, typeof(NullResponse))]
    [ProtoInclude(506, typeof(ServerDescriptionResponse))]
    [ProtoInclude(507, typeof(GenerateUniqueIdsResponse))]
    [ProtoInclude(508, typeof(ReadyResponse))]
    public abstract class Response
    {
        /// <summary>
        /// </summary>
        public abstract ResponseType ResponseType { get; }
    }
}