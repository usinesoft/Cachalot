using System;
using System.Collections.Generic;
using System.Text.Json;


namespace Client.Core;


public class RankedItem:IWithComparableMember, IRankedItem
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
            var comparable = (AsComparable)obj;
            var other = comparable._element;
            

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

    private readonly Dictionary<string, JsonElement> _valueByCaseInsensitiveName = new();

    public RankedItem(double rank, JsonDocument item)
    {
        // used by equals
        Json = item.RootElement.GetRawText();
        Rank = rank;
        Item = item;
        foreach (var child in item.RootElement.EnumerateObject())
        {
            _valueByCaseInsensitiveName[child.Name.ToLower()] = child.Value;
        }
        
    }

    public string Json { get; }

    public double Rank { get; }

    public JsonDocument Item { get; }


    public IComparable GetComparableMember(string name)
    {
        return new AsComparable(_valueByCaseInsensitiveName[name.ToLowerInvariant()]);
    }

    public override bool Equals(object obj)
    {
        if (obj is RankedItem other)
        {
            

            return Json.Equals(other.Json);
        }

        return false;
    }

    public override int GetHashCode()
    {
        return Json.GetHashCode();
    }
}

/// <summary>
/// Abstraction for both representations on json object
/// </summary>
public interface IWithComparableMember
{
    IComparable GetComparableMember(string name);
}

public interface IRankedItem
{
    public double Rank { get; }
}