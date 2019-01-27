using Client.ChannelInterface;
using ProtoBuf;

namespace Client.Messages
{
    /// <summary>
    ///     Reconstruct the database from a dump
    /// </summary>
    [ProtoContract]
    public class ImportDumpRequest : Request
    {
        [ProtoMember(1)] public string Path { get; set; }

        public override RequestClass RequestClass => RequestClass.Admin;

        /// <summary>
        ///     0 switch the servers to admin mode and move old persistence storage (if safe mode)
        ///     1 clear all data in memory, create new empty storage, import dump
        ///     2 destroy backup copies and switch the server to normal mode (everything worked fine)
        ///     3 restore the backup copies and restart the server (rollback)
        /// </summary>
        [ProtoMember(2)]
        public int Stage { get; set; }


        [ProtoMember(3)] public int ShardIndex { get; set; }
    }
}