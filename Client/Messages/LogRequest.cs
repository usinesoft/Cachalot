using Client.ChannelInterface;
using ProtoBuf;

namespace Client.Messages
{
    [ProtoContract]
    public class LogRequest : Request
    {
        [ProtoMember(1)] private int _linesCount;

        public LogRequest()
        {
        }

        public LogRequest(int linesCount)
        {
            LinesCount = linesCount;
        }

        public override RequestClass RequestClass => RequestClass.Admin;

        public int LinesCount
        {
            get => _linesCount;
            set => _linesCount = value;
        }
    }
}