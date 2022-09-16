#region

using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

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
        private byte[] _data;


        public OriginalType Type => (OriginalType)_data[0];

        public override string ToString()
        {
            var type = (OriginalType)_data[0];

            switch (type)
            {
                case OriginalType.SomeInteger:
                    return _hashCode.ToString();

                case OriginalType.SomeFloat:
                    return NumericValue.ToString(CultureInfo.InvariantCulture);

                case OriginalType.Boolean:
                    return (_hashCode != 0).ToString();

                case OriginalType.Date:
                    if (_data.Length == 1)//no offset
                        return new DateTime(_hashCode).ToString(CultureInfo.InvariantCulture);

                    var offset = BitConverter.ToInt64(_data, 1);
                    return new DateTimeOffset(_hashCode, new TimeSpan(offset)).ToString();

                case OriginalType.String:
                    return StringValue;

                case OriginalType.Null:
                    return "null";

                default:
                    throw new ArgumentOutOfRangeException();
            }

        }

        public JProperty ToJson(string name)
        {
            var type = (OriginalType)_data[0];

            switch (type)
            {
                case OriginalType.SomeInteger:
                    return new JProperty(name, _hashCode);

                case OriginalType.SomeFloat:
                    return new JProperty(name, NumericValue);

                case OriginalType.Boolean:
                    return new JProperty(name, _hashCode != 0);

                case OriginalType.Date:
                    if (_data.Length == 1)//no offset
                        return new JProperty(name, new DateTime(_hashCode));

                    var offset = BitConverter.ToInt64(_data, 1);

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
            _data[0] = (byte)type;
        }

        void FromFloatingPoint(double floatValue, OriginalType type)
        {
            _hashCode = (long)(floatValue * FloatingPrecision);

            _data[0] = (byte)type;

            // If no precision was lost, no need to keep the original value otherwise store it
            // If possible, storing it as an int mai handle the precision better than the double (1.7 for example 
            if (_hashCode % 10 != 0)
            {
                var original = BitConverter.GetBytes(floatValue);

                var data = new byte[1 + original.Length];
                data[0] = _data[0];

                Buffer.BlockCopy(original, 0, data, 1, original.Length);

                _data = data;
            }

        }

        void FromString(string stringValue)
        {

            StableHashForString(stringValue);

            _data[0] = (byte)OriginalType.String;

            var original = Encoding.UTF8.GetBytes(stringValue);

            var data = new byte[1 + original.Length];
            data[0] = _data[0];

            Buffer.BlockCopy(original, 0, data, 1, original.Length);

            _data = data;

        }

        void FromNull()
        {
            _hashCode = 0;

            _data[0] = (byte)OriginalType.Null;
        }

        public bool IsNull => _data[0] == (int)OriginalType.Null;

        /// <summary>
        /// This kind of date needs to be serialized to two longs to be reconstructed identically
        /// </summary>
        /// <param name="value"></param>
        private void FromDateTimeWithTimeZone(DateTimeOffset value)
        {
            _hashCode = value.Ticks;


            var offset = value.Offset.Ticks;

            if (offset == 0)
            {
                _data[0] = (byte)OriginalType.Date;
                return;
            }

            var offsetBytes = BitConverter.GetBytes(offset);

            _data = new byte[1 + offsetBytes.Length];
            _data[0] = (byte)OriginalType.Date;

            Buffer.BlockCopy(offsetBytes, 0, _data, 1, offsetBytes.Length);

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
            _data = new byte[1];

            //KeyName = info.Name;

            if (value == null)
            {
                FromNull();
                return;
            }

            var propertyType = value.GetType();


            //integer types
            if (propertyType == typeof(int) || propertyType == typeof(short) || propertyType == typeof(long) ||
                propertyType == typeof(byte) || propertyType == typeof(char))

            {
                var longVal = Convert.ToInt64(value);
                FromLong(longVal, OriginalType.SomeInteger);
                return;
            }

            if (propertyType == typeof(bool))
            {
                var longVal = Convert.ToInt64(value);
                FromLong(longVal, OriginalType.Boolean);
                return;
            }

            if (propertyType.IsEnum)
            {
                var longVal = Convert.ToInt64(value);
                FromLong(longVal, OriginalType.SomeInteger);
                return;
            }


            if (propertyType == typeof(float) || propertyType == typeof(double) || propertyType == typeof(decimal))
            {
                FromFloatingPoint(Convert.ToDouble(value), OriginalType.SomeFloat);
                return;
            }

            if (propertyType == typeof(DateTime))
            {
                // the default DateTime can not be directly converted to DateTimeOffset
                var date = (DateTime)value;
                if (date == default)
                {
                    FromDateTimeWithTimeZone(default);
                    return;
                }


                FromDateTimeWithTimeZone(new DateTimeOffset(date));

                return;
            }

            if (propertyType == typeof(DateTimeOffset))
            {
                FromDateTimeWithTimeZone((DateTimeOffset)value);
                return;
            }

            // ReSharper disable once PossibleNullReferenceException
            if (!propertyType.Namespace.StartsWith("System"))
            {
                throw new NotSupportedException($"Only system types are supported for server-side values. {propertyType.FullName} is not supported");
            }

            FromString(value.ToString());


        }

        private bool Equals(KeyValue other)
        {
            if (_hashCode != other._hashCode)
                return false;

            if (_hashCode == 0 && other._hashCode == 0)
            {
                // consider zero and null the same for indexing
                return true;
            }

            return _data.SequenceEqual(other._data);
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

            if ((OriginalType)_data[0] == OriginalType.String)
            {
                return string.Compare(StringValue, other.StringValue, StringComparison.Ordinal);
            }

            if (Type == other.Type)
            {
                return _hashCode.CompareTo(other._hashCode);
            }

            if (double.IsNaN(NumericValue) || double.IsNaN(other.NumericValue))
            {
                throw new NotSupportedException("Incompatible types for comparison");
            }

            return NumericValue.CompareTo(other.NumericValue);
        }


        public double NumericValue
        {
            get
            {
                var type = (OriginalType)_data[0];

                if (type == OriginalType.SomeInteger)
                {
                    return _hashCode;
                }

                if (type == OriginalType.SomeFloat)
                {
                    if (_hashCode % 10 == 0)
                    {
                        return _hashCode / FloatingPrecision;
                    }

                    return BitConverter.ToDouble(_data, 1);
                }

                return double.NaN;
            }
        }

        public string StringValue
        {
            get
            {
                var type = (OriginalType)_data[0];
                if (type != OriginalType.String)
                {
                    return null;
                }

                return Encoding.UTF8.GetString(_data, 1, _data.Length - 1);
            }
        }

        public long IntValue => _hashCode;

        public DateTimeOffset? DateValue
        {
            get
            {
                if (Type != OriginalType.Date)
                {
                    return null;
                }

                if (_data.Length == 1)//no offset
                    return new DateTimeOffset(new DateTime(_hashCode));

                var offset = BitConverter.ToInt64(_data, 1);
                return new DateTimeOffset(_hashCode, new TimeSpan(offset));

            }
        }

        JValue JsonValue
        {
            get
            {
                var type = (OriginalType)_data[0];

                switch (type)
                {
                    case OriginalType.SomeInteger:
                        return new JValue(_hashCode);

                    case OriginalType.SomeFloat:
                        return new JValue(NumericValue);

                    case OriginalType.Boolean:
                        return new JValue(_hashCode != 0);

                    case OriginalType.Date:
                        if (_data.Length == 1)//no offset
                            return new JValue(new DateTime(_hashCode));

                        var offset = BitConverter.ToInt64(_data, 1);
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