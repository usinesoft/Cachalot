using Newtonsoft.Json.Linq;

namespace Client.Core;

public class RankedItem
{
    private readonly JTokenEqualityComparer _comparer = new();

    public RankedItem(double rank, JObject item)
    {
        Rank = rank;
        Item = item;
    }

    public double Rank { get; }

    public JObject Item { get; }

    private bool Equals(RankedItem other)
    {
        return _comparer.Equals(Item, other.Item);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((RankedItem)obj);
    }

    public override int GetHashCode()
    {
        return _comparer.GetHashCode(Item);
    }
}