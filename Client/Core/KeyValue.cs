#region

using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Text;
using Client.Interface;
using Client.Tools;

#endregion

namespace Client.Core
{
    /// <summary>
    /// A value a seen server-side. This class is used for fast indexing (converted to long or string) and
    /// it is able to recreate the original value
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
            Null = 5,
        }

        private const double FloatingPrecision = 10000;



        [ProtoMember(1)]
        private long _hashCode;

        [ProtoMember(2)]
        private byte[] _data = Array.Empty<byte>();

        [ProtoMember(3)]
        private OriginalType _dataType;

        /// <summary>
        /// Mostly for diagnostic
        /// </summary>
        public int ExtraBytes => _data.Length;

        public OriginalType Type => _dataType;
        public bool IsNull => _dataType == OriginalType.Null;

        public double NumericValue
        {
            get
            {
                

                if (_dataType == OriginalType.SomeInteger)
                {
                    return _hashCode;
                }

                if (_dataType == OriginalType.SomeFloat)
                {
                    if (_hashCode % 10 == 0)
                    {
                        return _hashCode / FloatingPrecision;
                    }

                    return BitConverter.ToDouble(_data, 0);
                }

                return double.NaN;
            }
        }

        public string StringValue
        {
            get
            {
                
                if (_dataType != OriginalType.String)
                {
                    return null;
                }

                return Encoding.UTF8.GetString(_data, 0, _data.Length);
            }
        }

        public long IntValue => _hashCode;

        public DateTimeOffset? DateValue
        {
            get
            {
                if (_dataType != OriginalType.Date)
                {
                    return null;
                }

                if (_data.Length == 0)//no offset
                    return new DateTimeOffset(new DateTime(_hashCode));

                var offset = BitConverter.ToInt64(_data, 0);
                
                return new DateTimeOffset(_hashCode, new TimeSpan(offset));

            }
        }

        JValue JsonValue
        {
            get
            {
                

                switch (_dataType)
                {
                    case OriginalType.SomeInteger:
                        return new JValue(_hashCode);

                    case OriginalType.SomeFloat:
                        return new JValue(NumericValue);

                    case OriginalType.Boolean:
                        return new JValue(_hashCode != 0);

                    case OriginalType.Date:
                        if (_data.Length == 0)//no offset
                            return new JValue(new DateTime(_hashCode));

                        var offset = BitConverter.ToInt64(_data, 0);
                        return new JValue(new DateTimeOffset(_hashCode, new TimeSpan(offset)));

                    case OriginalType.String:
                        return new JValue(StringValue);

                    case OriginalType.Null:
                        return JValue.CreateNull();

                    default:
                        throw new ArgumentOutOfRangeException();
                }


            }
        }

        public override string ToString()
        {
            
            switch (_dataType)
            {
                case OriginalType.SomeInteger:
                    return _hashCode.ToString();

                case OriginalType.SomeFloat:
                    return NumericValue.ToString(CultureInfo.InvariantCulture);

                case OriginalType.Boolean:
                    return (_hashCode != 0).ToString();

                case OriginalType.Date:
                    if (_data.Length == 0)//no offset
                        return SmartDateTimeConverter.FormatDate( new DateTime(_hashCode));

                    var offset = BitConverter.ToInt64(_data, 0);
                    return new DateTimeOffset(_hashCode, new TimeSpan(offset)).ToString("yyyy-MM-dd");

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

            if (obj is int i) return _hashCode == i;

            if (obj is long l) return _hashCode == l;

            if (obj is string s) return StringValue == s;


            return Equals((KeyValue)obj);
        }

        public override int GetHashCode()
        {
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            return (int)(_hashCode % int.MaxValue);
        }

        public int CompareTo(KeyValue other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;


            // null is smaller than any other value
            if (Type == OriginalType.Null)
            {
                if (other.Type == OriginalType.Null)
                {
                    return 0;
                }

                return -1;
            }

            if (other.Type == OriginalType.Null)
            {
                return 1;
            }

            // the only different types that can be compared are float and integer
            if (Type == OriginalType.SomeInteger && other.Type == OriginalType.SomeFloat ||
                Type == OriginalType.SomeFloat && other.Type == OriginalType.SomeInteger)
            {
                return NumericValue.CompareTo(other.NumericValue);
            }

            if (Type != other.Type)
            {
                throw new CacheException($"Incompatible types for comparison:{Type} and {other.Type}");
            }

            if (Type == OriginalType.String )
            {
                return string.Compare(StringValue, other.StringValue, StringComparison.Ordinal);
            }

            // works for booleans, integers and dates
            return _hashCode.CompareTo(other._hashCode);
            
            
        }

        public JProperty ToJson(string name)
        {
            var type = _dataType;

            switch (type)
            {
                case OriginalType.SomeInteger:
                    return new JProperty(name, _hashCode);

                case OriginalType.SomeFloat:
                    return new JProperty(name, NumericValue);

                case OriginalType.Boolean:
                    return new JProperty(name, _hashCode != 0);

                case OriginalType.Date:
                    if (_data.Length == 0)//no offset
                        return new JProperty(name, new DateTime(_hashCode));

                    var offset = BitConverter.ToInt64(_data, 0);

                    return new JProperty(name, new DateTimeOffset(_hashCode, new TimeSpan(offset)));

                case OriginalType.String:
                    return new JProperty(name, StringValue);

                case OriginalType.Null:
                    return new JProperty(name, null);

                default:
                    throw new ArgumentOutOfRangeException();
            }

        }

        void StableHashForString(string value)
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

                _hashCode = Math.Abs(hash);
            }

        }

        void FromLong(long longValue, OriginalType type)
        {
            _hashCode = longValue;
            _dataType = type;
        }

        void FromFloatingPoint(double floatValue, OriginalType type)
        {
            _hashCode = (long)(floatValue * FloatingPrecision);

            _dataType = OriginalType.SomeFloat;

            //TODO use spans

            // If no precision was lost, no need to keep the original value otherwise store it
            // If possible, storing it as an int mai handle the precision better than the double (1.7 for example )
            if (_hashCode % 10 != 0)
            {
                var original = BitConverter.GetBytes(floatValue);

                var data = new byte[original.Length];
                
                Buffer.BlockCopy(original, 0, data, 0, original.Length);

                _data = data;
            }

        }

        void FromString(string stringValue)
        {

            StableHashForString(stringValue);

            //_hashCode = stringValue.GetHashCode();

            
            int estimatedSize = Encoding.UTF8.GetByteCount(stringValue);

            _data = new byte[estimatedSize];

            _dataType = OriginalType.String;

            Encoding.UTF8.GetBytes(stringValue, new Span<byte>(_data, 0, estimatedSize));

            
        }

        void FromNull()
        {
            _hashCode = 0;

            _dataType = OriginalType.Null;
        }

        /// <summary>
        /// This kind of date needs to be serialized to two longs to be reconstructed identically
        /// </summary>
        /// <param name="value"></param>
        private void FromDateTimeWithTimeZone(DateTimeOffset value)
        {
            _hashCode = value.Ticks;

            _dataType = OriginalType.Date;

            var offset = value.Offset.Ticks;

            if (offset == 0)
            {                
                return;
            }

            var offsetBytes = BitConverter.GetBytes(offset);

            _data = new byte[offsetBytes.Length];
            
            Buffer.BlockCopy(offsetBytes, 0, _data, 0, offsetBytes.Length);

        }



        /// <summary>
        /// For serialization only
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
                var longVal = b?1:0;
                FromLong(longVal, OriginalType.Boolean);
                return;
            }

            //integer types
            if(value is long l)
            {
                //var longVal = Convert.ToInt64(value);
                FromLong(l, OriginalType.SomeInteger);
                return;
            }

            if(value is int i)
            {
                FromLong(i, OriginalType.SomeInteger);
                return;
            }

            if(value is short s)
            {
                FromLong(s, OriginalType.SomeInteger);
                return;
            }

            if(value is char c)
            {
                
                FromLong(c, OriginalType.SomeInteger);
                return;
            }

            if(value is byte bt)
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


                FromDateTimeWithTimeZone(new DateTimeOffset(dt));

                return;
            }

            if (value is DateTimeOffset dto)
            {
                FromDateTimeWithTimeZone(dto);
                return;
            }

            FromString(value.ToString());

        }

        private bool Equals(KeyValue other)
        {
            if (_hashCode != other._hashCode)
                return false;

            if (this.Type != other.Type)
            {
                return false;
            }

            if (_hashCode == 0 && other._hashCode == 0)
            {
                // consider zero and null the same for indexing
                return true;
            }

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
    /// KeyValue with name (to minimize memory usage the name is not stored any more in the KeyValue)
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
}