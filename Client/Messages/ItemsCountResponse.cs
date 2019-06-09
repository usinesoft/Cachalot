using Client.ChannelInterface;
using ProtoBuf;

namespace Client.Messages
{
    [ProtoContract]
    public class ItemsCountResponse : Response
    {
        public ItemsCountResponse()
        {
        }

        public ItemsCountResponse(int items)
        {
            ItemsCount = items;
        }

        public override ResponseType ResponseType => ResponseType.Data;

        /// <summary>
        ///     Number of items in the result set
        /// </summary>
        [field: ProtoMember(1)]
        public int ItemsCount { get; set; }
    }
}