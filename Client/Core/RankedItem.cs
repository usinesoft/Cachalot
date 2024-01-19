using System;
using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace Client.Core;

public class RankedItem:IWithComparableMember
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

    public IComparable GetComparableMember(string name)
    {
        return (JValue)Item.GetValue(name, StringComparison.InvariantCultureIgnoreCase);
    }
}

public class RankedItem2:IWithComparableMember
{

    class AsComparable : IComparable
    {
        readonly JsonElement _element;

        public AsComparable(JsonElement element)
        {
            _element = element;
        }


        public int CompareTo(object obj)
        {
            var other = (JsonElement)obj;
            

            switch (other.ValueKind)
            {
                case JsonValueKind.String:
                    if (_element.ValueKind == JsonValueKind.String)
                    {
                        return String.Compare(_element.GetString(), other.GetString(), StringComparison.Ordinal);
                    }
                    break;
                case JsonValueKind.Number:
                    if (_element.ValueKind == JsonValueKind.Number)
                    {
                        return _element.GetDecimal().CompareTo(other.GetDecimal());
                    }
                    break;
                case JsonValueKind.True:
                    switch (_element.ValueKind)
                    {
                        case JsonValueKind.True:
                            return 0;
                            
                        case JsonValueKind.False:
                            return 1;
                    }
                    break;
                case JsonValueKind.False:
                    switch (_element.ValueKind)
                    {
                        case JsonValueKind.False:
                            return 0;
                            
                        case JsonValueKind.True:
                            return -1;
                    }
                    break;
                case JsonValueKind.Null:
                    if (_element.ValueKind == JsonValueKind.Null)
                        return 0;
                    return -1;
                    
            }

            throw new ArgumentException($"Cannot compare {_element} with {other}");
        }
    }
    
    public RankedItem2(double rank, JsonDocument item)
    {
        Rank = rank;
        Item = item;
    }

    public double Rank { get; }

    public JsonDocument Item { get; }


    public IComparable GetComparableMember(string name)
    {
        var value = Item.RootElement.GetProperty(name);

        return new AsComparable(value);
    }
}

/// <summary>
/// Abstraction for both representations on json object
/// </summary>
public interface IWithComparableMember
{
    IComparable GetComparableMember(string name);
}