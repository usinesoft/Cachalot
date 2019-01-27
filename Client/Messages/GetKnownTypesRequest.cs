using Client.ChannelInterface;
using ProtoBuf;

namespace Client.Messages
{
    /// <summary>
    ///     Ask the server for the registered types descriptions
    /// </summary>
    [ProtoContract]
    public class GetKnownTypesRequest : Request
    {
        public override RequestClass RequestClass => RequestClass.Admin;
    }
}