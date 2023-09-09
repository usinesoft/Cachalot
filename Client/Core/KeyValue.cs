#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Client.Interface;
using Client.Tools;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
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
        if (value is null)
        {
            FromNull();
            return;
        }


        if (value is Enum e)
        {
            var longVal = Convert.ToInt64(e);
            FromLong(longVal, OriginalType.SomeInteger);
            return;
        }

        if (value is bool b)
        {
            var longVal = b ? 1 : 0;
            FromLong(longVal, OriginalType.Boolean);
            return;
        }

        //integer types
        if (value is long l)
        {
            //var longVal = Convert.ToInt64(value);
            FromLong(l, OriginalType.SomeInteger);
            return;
        }

        if (value is int i)
        {
            FromLong(i, OriginalType.SomeInteger);
            return;
        }

        if (value is short s)
        {
            FromLong(s, OriginalType.SomeInteger);
            return;
        }

        if (value is char c)
        {
            FromLong(c, OriginalType.SomeInteger);
            return;
        }

        if (value is byte bt)
        {
            FromLong(bt, OriginalType.SomeInteger);
            return;
        }


        if (value is double d)
        {
            FromFloatingPoint(d, OriginalType.SomeFloat);
            return;
        }

        if (value is float f)
        {
            FromFloatingPoint(f, OriginalType.SomeFloat);
            return;
        }

        if (value is decimal de)
        {
            FromFloatingPoint((double)de, OriginalType.SomeFloat);
            return;
        }

        if (value is DateTime dt)
        {
            // the default DateTime can not be directly converted to DateTimeOffset

            if (dt == default)
            {
                FromDateTimeWithTimeZone(default);
                return;
            }


            FromDateTimeWithTimeZone(new(dt.Ticks, TimeSpan.Zero));

            return;
        }

        if (value is DateTimeOffset dto)
        {
            FromDateTimeWithTimeZone(dto);
            return;
        }

        FromString(value.ToString());
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

    private JValue JsonValue
    {
        get
        {
            switch (Type)
            {
                case OriginalType.SomeInteger:
                    return new(IntValue);

                case OriginalType.SomeFloat:
                    return new(NumericValue);

                case OriginalType.Boolean:
                    return new(IntValue != 0);

                case OriginalType.Date:
                    if (_data.Length == 0) //no offset
                        return new(new DateTime(IntValue));

                    var offset = BitConverter.ToInt64(_data, 0);
                    return new(new DateTimeOffset(IntValue, new(offset)));

                case OriginalType.String:
                    return new(StringValue);

                case OriginalType.Null:
                    return JValue.CreateNull();

                default:
                    throw new ArgumentOutOfRangeException();
            }
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
                    return SmartDateTimeConverter.FormatDate(new(IntValue));

                var offset = BitConverter.ToInt64(_data, 0);
                return new DateTimeOffset(IntValue, new(offset)).ToString("yyyy-MM-dd");

            case OriginalType.String:
                return StringValue;

            case OriginalType.Null:
                return "null";

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;

        if (obj is int i) return IntValue == i;

        if (obj is long l) return IntValue == l;

        if (obj is string s) return StringValue == s;


        return Equals((KeyValue)obj);
    }

    public override int GetHashCode()
    {
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        return (int)(IntValue % int.MaxValue);
    }

    public JProperty ToJson(string name)
    {
        var type = Type;

        switch (type)
        {
            case OriginalType.SomeInteger:
                return new(name, IntValue);

            case OriginalType.SomeFloat:
                return new(name, NumericValue);

            case OriginalType.Boolean:
                return new(name, IntValue != 0);

            case OriginalType.Date:
                if (_data.Length == 0) //no offset
                    return new(name, new DateTime(IntValue));

                var offset = BitConverter.ToInt64(_data, 0);

                return new(name, new DateTimeOffset(IntValue, new(offset)));

            case OriginalType.String:
                return new(name, StringValue);

            case OriginalType.Null:
                return new(name, null);

            default:
                throw new ArgumentOutOfRangeException();
        }
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

    private void FromLong(long longValue, OriginalType type)
    {
        IntValue = longValue;
        Type = type;
    }

    private void FromFloatingPoint(double floatValue, OriginalType type)
    {
        IntValue = (long)(floatValue * FloatingPrecision);

        Type = OriginalType.SomeFloat;

        //TODO use spans

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

        //_hashCode = stringValue.GetHashCode();


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

    private bool Equals(KeyValue other)
    {
        if (Type is OriginalType.SomeFloat or OriginalType.SomeInteger)
            if (other.Type is OriginalType.SomeFloat or OriginalType.SomeInteger)
                return Math.Abs(NumericValue - other.NumericValue) < double.Epsilon;


        if (IntValue != other.IntValue)
            return false;


        if (Type != other.Type) return false;

        if (IntValue == 0 && other.IntValue == 0)
            // consider zero and null the same for indexing
            return true;

        return _data.SequenceEqual(other._data);
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

/// <summary>
///     KeyValue with name (to minimize memory usage the name is not stored any more in the KeyValue)
/// </summary>
public class NamedValue
{
    public NamedValue(KeyValue value, string name)
    {
        Value = value;
        Name = name;
    }

    [field: ProtoMember(1)] public KeyValue Value { get; }
    [field: ProtoMember(2)] public string Name { get; }

    public override bool Equals(object obj)
    {
        return obj is NamedValue value &&
               EqualityComparer<KeyValue>.Default.Equals(Value, value.Value) &&
               Name == value.Name;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Value, Name);
    }
}