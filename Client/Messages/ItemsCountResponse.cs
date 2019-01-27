using Client.ChannelInterface;
using ProtoBuf;

namespace Client.Messages
{
    [ProtoContract]
    public class ItemsCountResponse : Response
    {
        [ProtoMember(1)] private int _items;

        public ItemsCountResponse()
        {
        }

        public ItemsCountResponse(int items)
        {
            _items = items;
        }

        public override ResponseType ResponseType => ResponseType.Data;

        /// <summary>
        ///     Number of items in the result set
        /// </summary>
        public int ItemsCount
        {
            get => _items;
            set => _items = value;
        }
    }
}