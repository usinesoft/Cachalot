using Client.ChannelInterface;
using ProtoBuf;

namespace Client.Messages
{
    [ProtoContract]
    public class LogRequest : Request
    {
        public LogRequest()
        {
        }

        public LogRequest(int linesCount)
        {
            LinesCount = linesCount;
        }

        public override RequestClass RequestClass => RequestClass.Admin;

        [field: ProtoMember(1)] public int LinesCount { get; set; }
    }
}