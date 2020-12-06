#region

using System;
using System.Linq;
using System.Text;
using Client.Interface;
using Client.Messages;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using ProtoBuf;

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
        

        enum OriginalType:byte
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

        [ProtoMember(3)]
        private readonly KeyInfo _info;


        public override string ToString()
        {
            return StringValue??IntValue.ToString();
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
            _data[0] = (byte) type;
        }

        void FromFloatingPoint(double floatValue, OriginalType type)
        {
            _hashCode = (long) (floatValue * FloatingPrecision);

            _data[0] = (byte) type;

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

            _data[0] = (byte) OriginalType.String;

            var original = Encoding.UTF8.GetBytes(stringValue);

            var data = new byte[1 + original.Length];
            data[0] = _data[0];

            Buffer.BlockCopy(original, 0, data, 1, original.Length);

            _data = data;

        }

        void FromNull()
        {
            _hashCode = 0;

            _data[0] = (byte) OriginalType.Null;
        }

        public bool IsNull => _data[0] == (int) OriginalType.Null;

        /// <summary>
        /// Thi kind of date needs to be serialized to two longs to be reconstructed identically
        /// </summary>
        /// <param name="value"></param>
        private void FromDateTimeWithTimeZone(DateTimeOffset value)
        {
            _hashCode = value.Ticks;

            _data[0] = (byte) OriginalType.Date;

            var offset = value.Offset.Ticks;

            var offsetBytes = BitConverter.GetBytes(offset);

            var data = new byte[1 + offsetBytes.Length];
            data[0] = _data[0];

            Buffer.BlockCopy(offsetBytes, 0, data, 1, offsetBytes.Length);

        }

        /// <summary>
        /// For serialization only
        /// </summary>
        [UsedImplicitly]
        public KeyValue()
        {
        }

        public KeyValue(string value, KeyInfo info):this((object)value, info)
        {

        }

        public KeyValue(long value, KeyInfo info):this((object)value, info)
        {

        }

        public KeyValue(object value, KeyInfo info)
        {
            _data = new byte[1];

            _info = info;
            if (value == null)
            {
                FromNull();
                return;
            }

            var propertyType = value.GetType();
            if (propertyType.Namespace != "System")
            {
                throw new NotSupportedException($"Only system types are supported for server-side values. {propertyType.FullName} is not supported");
            }

            //integer types
            if (propertyType == typeof(int) || propertyType == typeof(short) || propertyType == typeof(long) ||
                propertyType == typeof(byte) || propertyType == typeof(char) || propertyType == typeof(bool))
                
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
                FromFloatingPoint((double)value, OriginalType.SomeFloat);
            }

            if (propertyType == typeof(DateTime))
            {
                FromLong(((DateTime) value).Ticks, OriginalType.Date);
                return;
            }

            if (propertyType == typeof(DateTimeOffset))
            {
                FromDateTimeWithTimeZone((DateTimeOffset) value);
                return;
            }

            
            FromString(value.ToString());
            
            
        }

        private bool Equals(KeyValue other)
        {
            if (_hashCode != other._hashCode)
                return false;

            return _data.SequenceEqual(other._data);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            
            if (obj is int i) return _hashCode == i;

            if (obj is long l) return _hashCode == l;

            if (obj is string s) return StringValue == s;

         
            return Equals((KeyValue) obj);
        }

        public override int GetHashCode()
        {
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            return (int) (_hashCode % int.MaxValue);
        }

        public int CompareTo(KeyValue other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;

            if ((OriginalType) _data[0] == OriginalType.String)
            {
                return string.Compare(StringValue, other.StringValue, StringComparison.Ordinal);
            }
            return _hashCode.CompareTo(other._hashCode);
        }


        public double NumericValue
        {
            get
            {
                var type = (OriginalType) _data[0];

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
                var type = (OriginalType) _data[0];
                if (type != OriginalType.String)
                {
                    return null;
                }

                return Encoding.UTF8.GetString(_data, 1, _data.Length -1);
            }
        }

        public long IntValue => _hashCode;

        JValue JsonValue
        {
            get
            {
                var type = (OriginalType) _data[0];

                switch (type)
                {
                    case OriginalType.SomeInteger:
                        return new JValue(_hashCode);

                    case OriginalType.SomeFloat:
                        return new JValue(NumericValue);

                    case OriginalType.Boolean:
                        return new JValue(_hashCode != 0);

                    case OriginalType.Date:
                        if(_data.Length == 1)//no offset
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

       

        #region compatibility

        public KeyDataType KeyDataType
        {
            get
            {
                var type = (OriginalType)_data[0];
                if (type == OriginalType.String)
                    return KeyDataType.StringKey;

                return KeyDataType.IntKey;
            }
        }

        /// <summary>
        ///     name of the key
        /// </summary>
        public string KeyName => _info.Name;

        /// <summary>
        ///     uniqueness of the key (primary, unique, index)
        /// </summary>
        public KeyType KeyType => _info.KeyType;

        #endregion
    }

//    /// <summary>
//    ///     A key needs to be convertible in a meaningful way to string or long int
//    ///     Using int keys is faster. Key values can not be modified once created which allows to pre compute the hashcode
//    /// </summary>
//    [ProtoContract]
//    public sealed class KeyValue : IComparable<KeyValue>
//    {
//        [ProtoMember(1)] private readonly KeyInfo _type;

//        [ProtoMember(2)] private readonly string _stringValue;

//        [ProtoMember(3)] private readonly long _intValue;

//        [ProtoMember(4)] private readonly int _hash; //precomputed hashcode

//        [ProtoMember(5)] private readonly bool _isNotNull;

//        [UsedImplicitly]
//        public KeyValue()
//        {
//        }

//        /// <summary>
//        ///     Copy constructor
//        /// </summary>
//        /// <param name="right"> </param>
//        public KeyValue(KeyValue right)
//        {
//            _hash = right._hash;
//            _intValue = right._intValue;
//            _stringValue = right._stringValue;
//            _type = new KeyInfo(right._type);
//            _isNotNull = right._isNotNull;
//        }

//        /// <summary>
//        ///     Create from string value and metadata
//        /// </summary>
//        /// <param name="value"> </param>
//        /// <param name="info"> </param>
//        public KeyValue(string value, KeyInfo info)
//        {
//            if (info.KeyType == KeyType.Primary)
//                if (value != null && value.StartsWith("#"))
//                    throw new NotSupportedException("A string primary key can not begin with #");

//            _stringValue = value;
//            _type = info;

//            if (value == null)
//            {
//                _hash = 0;
//            }

//            else //manually compute the hash code to ensure interoperability between 64 and 32 bits systems
//            {
//                long hash = 1;
//                long multiplier = 29;
//                foreach (var c in value)
//                {
//                    hash += c * multiplier;
//                    multiplier *= multiplier;
//                }

//                hash = Math.Abs(hash);
//                _hash = (int) (hash % int.MaxValue);
//            }

//            _isNotNull = true;
//        }

//        /// <summary>
//        ///     Create from long value and metadata
//        /// </summary>
//        /// <param name="value"> </param>
//        /// <param name="info"> </param>
//        public KeyValue(long value, KeyInfo info)
//        {
//            _intValue = value;
//            _type = info;
//            _hash = Math.Abs(value.GetHashCode() % int.MaxValue);
//            _isNotNull = true;
//        }

       

//        /// <summary>
//        ///     string or int
//        /// </summary>
//        public KeyDataType KeyDataType => _stringValue != null ? KeyDataType.StringKey : KeyDataType.IntKey;

//        /// <summary>
//        ///     name of the key
//        /// </summary>
//        public string KeyName => _type.Name;

//        /// <summary>
//        ///     uniqueness of the key (primary, unique, index)
//        /// </summary>
//        public KeyType KeyType => _type.KeyType;

//        #region IComparable<KeyValue> Members

//        public int CompareTo(KeyValue other)
//        {
//            if (_stringValue != null)
//                return string.Compare(_stringValue, other._stringValue, StringComparison.Ordinal);
//            return _intValue > other._intValue ? 1 :
//                _intValue < other._intValue ? -1 : 0;
//        }

//        #endregion


//        public override string ToString()
//        {
//            if (!_isNotNull)
//                return "<null>";

//            if (_stringValue != null)
//                return _stringValue;

//            return $"#{_intValue}";
//        }

//        public override bool Equals(object obj)
//        {
//            if (obj == null)
//                return false;

//            if (obj is KeyValue right)
//                return KeyName == right.KeyName && right._stringValue == _stringValue &&
//                       right._intValue == _intValue;

//            if (obj is int i) return _intValue == i;

//            if (obj is long l) return _intValue == l;


//            if (obj is string s) return _stringValue == s;

//            return false;
//        }


//        public override int GetHashCode()
//        {
//            return _hash;
//        }


//        /// <summary>
//        ///     Compare to string
//        /// </summary>
//        /// <param name="left"> </param>
//        /// <param name="right"> </param>
//        /// <returns> </returns>
//        public static bool operator ==(KeyValue left, string right)
//        {
//            if (left?._stringValue == null)
//                return false;
//            return left._stringValue == right;
//        }

//        /// <summary>
//        ///     Compare to string
//        /// </summary>
//        /// <param name="left"> </param>
//        /// <param name="right"> </param>
//        /// <returns> </returns>
//        public static bool operator !=(KeyValue left, string right)
//        {
//            return !(left == right);
//        }


//        /// <summary>
//        ///     Compare to long
//        /// </summary>
//        /// <param name="left"> </param>
//        /// <param name="right"> </param>
//        /// <returns> </returns>
//        public static bool operator ==(KeyValue left, long right)
//        {
//            if (left?._intValue == long.MinValue)
//                return false;
//            return left?._intValue == right;
//        }

//        /// <summary>
//        ///     Compare to long
//        /// </summary>
//        /// <param name="left"> </param>
//        /// <param name="right"> </param>
//        /// <returns> </returns>
//        public static bool operator !=(KeyValue left, long right)
//        {
//            return !(left == right);
//        }

//        /// <summary>
//        /// </summary>
//        /// <param name="left"> </param>
//        /// <param name="right"> </param>
//        /// <returns> </returns>
//        public static bool operator ==(KeyValue left, KeyValue right)
//        {
//            return Equals(left, right);
//        }

//        /// <summary>
//        /// </summary>
//        /// <param name="left"> </param>
//        /// <param name="right"> </param>
//        /// <returns> </returns>
//        public static bool operator !=(KeyValue left, KeyValue right)
//        {
//            return !(left == right);
//        }

//        /// <summary>
//        /// </summary>
//        /// <param name="left"> </param>
//        /// <param name="right"> </param>
//        /// <returns> </returns>
//        public static bool operator <=(KeyValue left, KeyValue right)
//        {
//            return left.CompareTo(right) <= 0;
//        }

//        /// <summary>
//        /// </summary>
//        /// <param name="left"> </param>
//        /// <param name="right"> </param>
//        /// <returns> </returns>
//        public static bool operator >=(KeyValue left, KeyValue right)
//        {
//            return left.CompareTo(right) >= 0;
//        }

//        /// <summary>
//        /// </summary>
//        /// <param name="left"> </param>
//        /// <param name="right"> </param>
//        /// <returns> </returns>
//        public static bool operator <(KeyValue left, KeyValue right)
//        {
//            return left.CompareTo(right) < 0;
//        }

//        /// <summary>
//        /// </summary>
//        /// <param name="left"> </param>
//        /// <param name="right"> </param>
//        /// <returns> </returns>
//        public static bool operator >(KeyValue left, KeyValue right)
//        {
//            return left.CompareTo(right) > 0;
//        }

//        /// <summary>
//        /// Readable key value for keys that where int or string (not meaningful for dates, floats)
//        /// </summary>
//        public string AxisValue => KeyDataType == KeyDataType.IntKey ? _intValue.ToString() : _stringValue;
//    }
}