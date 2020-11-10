using ProtoBuf;

namespace Client.Core
{

    /// <summary>
    /// Tag a property which can be used for server-side calculations
    /// </summary>
    [ProtoContract]
    public sealed class ServerSideValue
    {
        [ProtoMember(1)]public string Name { get; set; }

        [ProtoMember(2)]public decimal Value { get; set; }
    }
}