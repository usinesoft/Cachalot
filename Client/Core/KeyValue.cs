#region

using System;
using Client.Interface;
using Client.Messages;
using JetBrains.Annotations;
using ProtoBuf;

#endregion

namespace Client.Core
{
    /// <summary>
    ///     A key needs to be convertible in a meaningful way to string or long int
    ///     Using int keys is faster. Key values can not be modified once created which allows to pre compute the hashcode
    /// </summary>
    [ProtoContract]
    public sealed class KeyValue : IComparable<KeyValue>
    {
        [ProtoMember(1)] private readonly KeyInfo _type;

        [ProtoMember(2)] private readonly string _stringValue;

        [ProtoMember(3)] private readonly long _intValue;

        [ProtoMember(4)] private readonly int _hash; //precomputed hashcode

        [ProtoMember(5)] private readonly bool _isNotNull;

        [UsedImplicitly]
        public KeyValue()
        {
        }

        /// <summary>
        ///     Copy constructor
        /// </summary>
        /// <param name="right"> </param>
        public KeyValue(KeyValue right)
        {
            _hash = right._hash;
            _intValue = right._intValue;
            _stringValue = right._stringValue;
            _type = new KeyInfo(right._type);
            _isNotNull = right._isNotNull;
        }

        /// <summary>
        ///     Create from string value and metadata
        /// </summary>
        /// <param name="value"> </param>
        /// <param name="info"> </param>
        public KeyValue(string value, KeyInfo info)
        {
            if (info.KeyType == KeyType.Primary)
                if (value != null && value.StartsWith("#"))
                    throw new NotSupportedException("A string primary key can not begin with #");

            _stringValue = value;
            _type = info;

            if (value == null)
            {
                _hash = 0;
            }

            else //manually compute the hash code to ensure interoperability between 64 and 32 bits systems
            {
                long hash = 1;
                long multiplier = 29;
                foreach (var c in value)
                {
                    hash += c * multiplier;
                    multiplier *= multiplier;
                }

                hash = Math.Abs(hash);
                _hash = (int) (hash % int.MaxValue);
            }

            _isNotNull = true;
        }

        /// <summary>
        ///     Create from long value and metadata
        /// </summary>
        /// <param name="value"> </param>
        /// <param name="info"> </param>
        public KeyValue(long value, KeyInfo info)
        {
            _intValue = value;
            _type = info;
            _hash = Math.Abs(value.GetHashCode() % int.MaxValue);
            _isNotNull = true;
        }


        /// <summary>
        ///     string or int
        /// </summary>
        public KeyDataType KeyDataType => _stringValue != null ? KeyDataType.StringKey : KeyDataType.IntKey;

        /// <summary>
        ///     name of the key
        /// </summary>
        public string KeyName => _type.Name;

        /// <summary>
        ///     uniqueness of the key (primary, unique, index)
        /// </summary>
        public KeyType KeyType => _type.KeyType;

        #region IComparable<KeyValue> Members

        public int CompareTo(KeyValue other)
        {
            if (_stringValue != null)
                return string.Compare(_stringValue, other._stringValue, StringComparison.Ordinal);
            return _intValue > other._intValue ? 1 :
                _intValue < other._intValue ? -1 : 0;
        }

        #endregion


        public override string ToString()
        {
            if (!_isNotNull)
                return "<null>";

            if (_stringValue != null)
                return _stringValue;

            return $"#{_intValue}";
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (obj is KeyValue right)
                return KeyName == right.KeyName && right._stringValue == _stringValue &&
                       right._intValue == _intValue;

            if (obj is int i) return _intValue == i;

            if (obj is long l) return _intValue == l;


            if (obj is string s) return _stringValue == s;

            return false;
        }


        public override int GetHashCode()
        {
            return _hash;
        }


        /// <summary>
        ///     Compare to string
        /// </summary>
        /// <param name="left"> </param>
        /// <param name="right"> </param>
        /// <returns> </returns>
        public static bool operator ==(KeyValue left, string right)
        {
            if (left?._stringValue == null)
                return false;
            return left._stringValue == right;
        }

        /// <summary>
        ///     Compare to string
        /// </summary>
        /// <param name="left"> </param>
        /// <param name="right"> </param>
        /// <returns> </returns>
        public static bool operator !=(KeyValue left, string right)
        {
            return !(left == right);
        }


        /// <summary>
        ///     Compare to long
        /// </summary>
        /// <param name="left"> </param>
        /// <param name="right"> </param>
        /// <returns> </returns>
        public static bool operator ==(KeyValue left, long right)
        {
            if (left?._intValue == long.MinValue)
                return false;
            return left?._intValue == right;
        }

        /// <summary>
        ///     Compare to long
        /// </summary>
        /// <param name="left"> </param>
        /// <param name="right"> </param>
        /// <returns> </returns>
        public static bool operator !=(KeyValue left, long right)
        {
            return !(left == right);
        }

        /// <summary>
        /// </summary>
        /// <param name="left"> </param>
        /// <param name="right"> </param>
        /// <returns> </returns>
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
}