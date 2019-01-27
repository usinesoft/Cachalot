using System.Collections.Generic;
using Client.ChannelInterface;
using ProtoBuf;

namespace Client.Messages
{
    [ProtoContract]
    public class LogResponse : Response
    {
        [ProtoMember(1)] private readonly List<ServerLogEntry> _entries = new List<ServerLogEntry>();
        [ProtoMember(2)] private ServerLogEntry _maxLockEntry;

        public IList<ServerLogEntry> Entries => _entries;

        public ServerLogEntry MaxLockEntry
        {
            get => _maxLockEntry;
            set => _maxLockEntry = value;
        }

        public override ResponseType ResponseType => ResponseType.Data;
    }
}