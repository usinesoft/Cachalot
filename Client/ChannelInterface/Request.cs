using Client.Messages;
using ProtoBuf;

namespace Client.ChannelInterface
{
    /// <summary>
    ///     Base abstract class for requests (messages sent from the client to the server)
    /// </summary>
    [ProtoContract]
    [ProtoInclude(500, typeof(DataRequest))]
    [ProtoInclude(501, typeof(GetKnownTypesRequest))]
    [ProtoInclude(502, typeof(LogRequest))]
    [ProtoInclude(503, typeof(RegisterTypeRequest))]
    [ProtoInclude(504, typeof(DumpRequest))]
    [ProtoInclude(505, typeof(ImportDumpRequest))]
    [ProtoInclude(506, typeof(GenerateUniqueIdsRequest))]
    [ProtoInclude(507, typeof(ResyncUniqueIdsRequest))]
    [ProtoInclude(508, typeof(SwitchModeRequest))]
    [ProtoInclude(509, typeof(StopRequest))]
    [ProtoInclude(510, typeof(DropRequest))]
    [ProtoInclude(511, typeof(TransactionRequest))]
    [ProtoInclude(512, typeof(ContinueRequest))]
    [ProtoInclude(513, typeof(LockRequest))]
    public abstract class Request
    {
        /// <summary>
        ///     Generic class of the request
        /// </summary>
        public abstract RequestClass RequestClass { get; }

        /// <summary>
        ///     If true, its a simple request + response dialog
        /// </summary>
        public virtual bool IsSimple => true;
    }
}