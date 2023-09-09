using Client.ChannelInterface;
using ProtoBuf;

namespace Client.Messages;

[ProtoContract]
public class EvalResponse : Response
{
    public override ResponseType ResponseType => ResponseType.Data;

    /// <summary>
    ///     Number of items in the result set
    /// </summary>
    [field: ProtoMember(2)]
    public int Items { get; set; }

    /// <summary>
    ///     Is the query result completelly available in the cache
    /// </summary>
    [field: ProtoMember(1)]
    public bool Complete { get; set; }
}