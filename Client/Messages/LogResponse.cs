using System.Collections.Generic;
using Client.ChannelInterface;
using ProtoBuf;

namespace Client.Messages
{
    [ProtoContract]
    public class LogResponse : Response
    {
        [ProtoMember(1)] private readonly List<ServerLogEntry> _entries = new List<ServerLogEntry>();

        public IList<ServerLogEntry> Entries => _entries;

        [field: ProtoMember(2)] public ServerLogEntry MaxLockEntry { get; set; }

        public override ResponseType ResponseType => ResponseType.Data;
    }
}