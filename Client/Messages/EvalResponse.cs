using Client.ChannelInterface;
using ProtoBuf;

namespace Client.Messages
{
    [ProtoContract]
    public class EvalResponse : Response
    {
        [ProtoMember(1)] private bool _complete;

        [ProtoMember(2)] private int _items;

        public override ResponseType ResponseType => ResponseType.Data;

        /// <summary>
        ///     Number of items in the result set
        /// </summary>
        public int Items
        {
            get => _items;
            set => _items = value;
        }

        /// <summary>
        ///     Is the query result completelly available in the cache
        /// </summary>
        public bool Complete
        {
            get => _complete;
            set => _complete = value;
        }
    }
}