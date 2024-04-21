#region

using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using Client.Interface;
using Client.Tools;
using JetBrains.Annotations;
using ProtoBuf;

#endregion

namespace Client.Core;

/// <summary>
///     A value a seen server-side. This class is used for fast indexing (converted to long or string) and
///     it is able to recreate the original value
/// </summary>
[ProtoContract]
public sealed class KeyValue : IComparable<KeyValue>
{
    public enum OriginalType : byte
    {
        SomeInteger = 0,
        SomeFloat = 1,
        Boolean = 2,
        Date = 3,
        String = 4,
        Null = 5
    }

    private const double FloatingPrecision = 10000;

    [ProtoMember(2)] private byte[] _data = Array.Empty<byte>();


    /// <summary>
    ///     For serialization only
    /// </summary>
    [UsedImplicitly]
    public KeyValue()
    {
    }


    public KeyValue(object value)
    {
        switch (value)
        {
            case null:
                FromNull();
                return;
            case Enum e:
            {
                var longVal = Convert.ToInt64(e);
                FromLong(longVal, OriginalType.SomeInteger);
                return;
            }
            case bool b:
            {
                var longVal = b ? 1 : 0;
                FromLong(longVal, OriginalType.Boolean);
                return;
            }
            //integer types
            case long l:
                FromLong(l, OriginalType.SomeInteger);
                return;
            case int i:
                FromLong(i, OriginalType.SomeInteger);
                return;
            case short s:
                FromLong(s, OriginalType.SomeInteger);
                return;
            case char c:
                FromLong(c, OriginalType.SomeInteger);
                return;
            case byte bt:
                FromLong(bt, OriginalType.SomeInteger);
                return;
            case double d:
                FromFloatingPoint(d);
                return;
            case float f:
                FromFloatingPoint(f);
                return;
            case decimal de:
                FromFloatingPoint((double)de);
                return;
            // the default DateTime can not be directly converted to DateTimeOffset
            case DateTime dt when dt == default:
                FromDateTimeWithTimeZone(default);
                return;
            // ignore the offset if simple date (tough, but I think good decision)
            case DateTime dt:
            {
                if (dt == dt.Date || dt.Kind == DateTimeKind.Unspecified)
                {
                    FromDateTimeWithTimeZone(new(dt.Ticks, TimeSpan.Zero));
                }
                else
                {
                    DateTimeOffset offset = dt;
                    FromDateTimeWithTimeZone(new(offset.Ticks, offset.Offset));
                }

                return;
            }
            case DateTimeOffset dto:
                FromDateTimeWithTimeZone(dto);
                return;
            default:
                FromString(value.ToString());
                break;
        }
    }

    /// <summary>
    ///     Mostly for diagnostic
    /// </summary>
    public int ExtraBytes => _data.Length;

    [field: ProtoMember(3)] public OriginalType Type { get; private set; }

    public bool IsNull => Type == OriginalType.Null;

    public double NumericValue
    {
        get
        {
            if (Type == OriginalType.SomeInteger) return IntValue;

            if (Type == OriginalType.SomeFloat)
            {
                if (IntValue % 10 == 0) return IntValue / FloatingPrecision;

                return BitConverter.ToDouble(_data, 0);
            }

            return double.NaN;
        }
    }

    public string StringValue
    {
        get
        {
            if (Type != OriginalType.String) return null;

            return Encoding.UTF8.GetString(_data, 0, _data.Length);
        }
    }

    [field: ProtoMember(1)] public long IntValue { get; private set; }

    public DateTimeOffset? DateValue
    {
        get
        {
            if (Type != OriginalType.Date) return null;

            if (_data.Length == 0) //no offset
                return new DateTimeOffset(IntValue, TimeSpan.Zero);

            var offset = BitConverter.ToInt64(_data, 0);

            return new DateTimeOffset(IntValue, new(offset));
        }
    }

    public int CompareTo(KeyValue other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (ReferenceEquals(null, other)) return 1;


        // null is smaller than any other value
        if (Type == OriginalType.Null)
        {
            if (other.Type == OriginalType.Null) return 0;

            return -1;
        }

        if (other.Type == OriginalType.Null) return 1;

        // the only different types that can be compared are float and integer
        if ((Type == OriginalType.SomeInteger && other.Type == OriginalType.SomeFloat) ||
            (Type == OriginalType.SomeFloat && other.Type == OriginalType.SomeInteger))
            return NumericValue.CompareTo(other.NumericValue);

        if (Type != other.Type) throw new CacheException($"Incompatible types for comparison:{Type} and {other.Type}");

        if (Type == OriginalType.String)
            return string.Compare(StringValue, other.StringValue, StringComparison.Ordinal);

        // works for booleans, integers and dates
        return IntValue.CompareTo(other.IntValue);
    }

    public override string ToString()
    {
        switch (Type)
        {
            case OriginalType.SomeInteger:
                return IntValue.ToString();

            case OriginalType.SomeFloat:
                return NumericValue.ToString(CultureInfo.InvariantCulture);

            case OriginalType.Boolean:
                return (IntValue != 0).ToString();

            case OriginalType.Date:
                if (_data.Length == 0) //no offset
                {
                    var date = new DateTime(IntValue);
                    
                    return date == date.Date
                        ? date.ToString("yyyy-MM-dd")
                        : date.ToString("yyyy-MM-dd HH:mm:ss");
                    
                }

                var offset = BitConverter.ToInt64(_data, 0);
                return new DateTimeOffset(IntValue, new(offset)).ToString("o");

            case OriginalType.String:
                return StringValue;

            case OriginalType.Null:
                return "null";

            default:
                return StringValue;
        }
    }

    

    public JsonValue ToJsonValue()
    {
        var type = Type;

        switch (type)
        {
            case OriginalType.SomeInteger:
                return JsonValue.Create(IntValue);

            case OriginalType.SomeFloat:
                return JsonValue.Create(NumericValue);

            case OriginalType.Boolean:
                return JsonValue.Create(IntValue != 0);

            case OriginalType.Date:
                if (_data.Length == 0) //no offset
                    return JsonValue.Create(new DateTime(IntValue, DateTimeKind.Unspecified));

                var offset = BitConverter.ToInt64(_data, 0);

                return JsonValue.Create(new DateTimeOffset(IntValue, new(offset)));

            case OriginalType.String:
                return JsonValue.Create(StringValue);

            case OriginalType.Null:
                return null;

            default:
                return JsonValue.Create(StringValue);
        }
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;

        return obj switch
        {
            int i => IntValue == i,
            long l => IntValue == l,
            string s => StringValue == s,
            _ => Equals((KeyValue)obj)
        };
    }

    public bool IsNumericValue => Type is OriginalType.SomeInteger or OriginalType.SomeFloat;

    private bool Equals(KeyValue other)
    {
        if (Type == OriginalType.SomeInteger && other.Type == OriginalType.SomeInteger)
        {
            return IntValue == other.IntValue;
        }
        
        if (IsNumericValue && other.IsNumericValue)
            return Math.Abs(NumericValue - other.NumericValue) < double.Epsilon;
        
        if (IntValue != other.IntValue)
            return false;


        if (Type != other.Type) return false;

        if (IntValue == 0 && other.IntValue == 0)
            // consider zero and null the same for indexing
            return true;

        return _data.SequenceEqual(other._data);
    }

    private void StableHashForString(string value)
    {
        unchecked
        {
            long hash = 1;
            long multiplier = 29;
            foreach (var c in value)
            {
                hash += c * multiplier;
                multiplier *= multiplier;
            }

            IntValue = Math.Abs(hash);
        }
    }

    public override int GetHashCode()
    {
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        unchecked
        {
            return (int)(IntValue % Int32.MaxValue);
        }
    }

    private void FromLong(long longValue, OriginalType type)
    {
        IntValue = longValue;
        Type = type;
    }

    private void FromFloatingPoint(double floatValue)
    {
        IntValue = (long)(floatValue * FloatingPrecision);

        Type = OriginalType.SomeFloat;


        // If no precision was lost, no need to keep the original value otherwise store it
        // If possible, storing it as an int mai handle the precision better than the double (1.7 for example )
        if (IntValue % 10 != 0)
        {
            var original = BitConverter.GetBytes(floatValue);

            var data = new byte[original.Length];

            Buffer.BlockCopy(original, 0, data, 0, original.Length);

            _data = data;
        }
    }

    private void FromString(string stringValue)
    {
        StableHashForString(stringValue);


        var estimatedSize = Encoding.UTF8.GetByteCount(stringValue);

        _data = new byte[estimatedSize];

        Type = OriginalType.String;

        Encoding.UTF8.GetBytes(stringValue, new(_data, 0, estimatedSize));
    }

    private void FromNull()
    {
        IntValue = 0;

        Type = OriginalType.Null;
    }

    /// <summary>
    ///     This kind of date needs to be serialized to two longs to be reconstructed identically
    /// </summary>
    /// <param name="value"></param>
    private void FromDateTimeWithTimeZone(DateTimeOffset value)
    {
        IntValue = value.Ticks;

        Type = OriginalType.Date;

        var offset = value.Offset.Ticks;

        if (offset == 0) return;

        var offsetBytes = BitConverter.GetBytes(offset);

        _data = new byte[offsetBytes.Length];

        Buffer.BlockCopy(offsetBytes, 0, _data, 0, offsetBytes.Length);
    }


    public static bool operator ==(KeyValue left, KeyValue right)
    {
        return Equals(left, right);
    }

    /// <summary>
    /// </summary>
    /// <param name="left"> </param>
    /// <param name="right"> </param>
    /// <returns> </returns>
    public static bool operator !=(KeyValue left, KeyValue right)
    {
        return !(left == right);
    }

    /// <summary>
    /// </summary>
    /// <param name="left"> </param>
    /// <param name="right"> </param>
    /// <returns> </returns>
    public static bool operator <=(KeyValue left, KeyValue right)
    {
        return left.CompareTo(right) <= 0;
    }

    /// <summary>
    /// </summary>
    /// <param name="left"> </param>
    /// <param name="right"> </param>
    /// <returns> </returns>
    public static bool operator >=(KeyValue left, KeyValue right)
    {
        return left.CompareTo(right) >= 0;
    }

    /// <summary>
    /// </summary>
    /// <param name="left"> </param>
    /// <param name="right"> </param>
    /// <returns> </returns>
    public static bool operator <(KeyValue left, KeyValue right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <summary>
    /// </summary>
    /// <param name="left"> </param>
    /// <param name="right"> </param>
    /// <returns> </returns>
    public static bool operator >(KeyValue left, KeyValue right)
    {
        return left.CompareTo(right) > 0;
    }
}