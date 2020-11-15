using ProtoBuf;

namespace Client.Messages.Pivot
{
    [ProtoContract]
    public class AggregatedValue
    {
        [field: ProtoMember(1)]public string ColumnName { get; set; }

        [field: ProtoMember(2)]public int Count{get; set; }
        
        [field: ProtoMember(3)]public decimal Sum{get; set; }

        public override string ToString()
        {
            return $"{nameof(ColumnName)}: {ColumnName}, {nameof(Count)}: {Count}, {nameof(Sum)}: {Sum}";
        }
    }
}