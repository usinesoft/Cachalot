#region

using System;
using System.Reflection;
using Client.Core;
using Client.Interface;
using JetBrains.Annotations;
using ProtoBuf;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

#endregion

namespace Client.Messages
{
    /// <summary>
    ///     Serializable version of <see cref="ClientSideKeyInfo" />
    ///     As <see cref="ClientSideKeyInfo" /> is attached to a <see cref="PropertyInfo" /> it can not be deserialized
    ///     in a context where the concrete type is not available
    ///     This class is immutable
    /// </summary>
    [ProtoContract]
    public class KeyInfo : IEquatable<KeyInfo>
    {
        [UsedImplicitly]
        public KeyInfo()
        {
        }


        /// <summary>
        ///     Public constructor for non ordered keys
        /// </summary>
        /// <param name="keyDataType"> </param>
        /// <param name="keyType"> </param>
        /// <param name="name"> </param>
        /// <param name="isOrdered"></param>
        /// <param name="isFullText"></param>
        public KeyInfo(KeyDataType keyDataType, KeyType keyType, string name, bool isOrdered = false,
            bool isFullText = false)
        {
            KeyDataType = keyDataType;
            KeyType = keyType;
            Name = name;
            IsOrdered = isOrdered;
            IsFullTextIndexed = isFullText;
        }


        /// <summary>
        ///     Copy constructor
        /// </summary>
        /// <param name="right"> </param>
        public KeyInfo(KeyInfo right)
        {
            if (right == null) throw new ArgumentNullException(nameof(right));

            KeyDataType = right.KeyDataType;
            IsOrdered = right.IsOrdered;
            KeyType = right.KeyType;
            Name = right.Name;
            IsFullTextIndexed = right.IsFullTextIndexed;
        }

        /// <summary>
        ///     long or string as specified by <see cref="KeyDataType" />
        /// </summary>
        [ProtoMember(1)]
        public KeyDataType KeyDataType { get; set; }

        /// <summary>
        ///     Uniqueness of the key as specified by <see cref="KeyType" />
        /// </summary>
        [ProtoMember(2)]
        public KeyType KeyType { get; set; }

        /// <summary>
        ///     Key name. Unique inside a cacheable type
        /// </summary>
        [ProtoMember(3)]
        public string Name { get; set; }

        /// <summary>
        ///     Used only for index values. If the index is ordered, order operators can be applied
        /// </summary>
        [ProtoMember(4)]
        public bool IsOrdered { get; set; }

        [ProtoMember(5)] public bool IsFullTextIndexed { get; set; }

        public bool Equals(KeyInfo keyInfo)
        {
            if (keyInfo == null) return false;
            if (!Equals(KeyDataType, keyInfo.KeyDataType)) return false;
            if (!Equals(KeyType, keyInfo.KeyType)) return false;
            if (!Equals(Name, keyInfo.Name)) return false;
            if (!Equals(IsOrdered, keyInfo.IsOrdered)) return false;
            return true;
        }

        /// <summary>
        /// </summary>
        /// <param name="keyInfo1"> </param>
        /// <param name="keyInfo2"> </param>
        /// <returns> </returns>
        public static bool operator !=(KeyInfo keyInfo1, KeyInfo keyInfo2)
        {
            return !Equals(keyInfo1, keyInfo2);
        }

        /// <summary>
        /// </summary>
        /// <param name="keyInfo1"> </param>
        /// <param name="keyInfo2"> </param>
        /// <returns> </returns>
        public static bool operator ==(KeyInfo keyInfo1, KeyInfo keyInfo2)
        {
            return Equals(keyInfo1, keyInfo2);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as KeyInfo);
        }

        public override int GetHashCode()
        {
            var result = KeyDataType.GetHashCode();
            result = 29 * result + KeyType.GetHashCode();
            result = 29 * result + Name.GetHashCode();
            result = 29 * result + IsOrdered.GetHashCode();
            return result;
        }


        public override string ToString()
        {
            return
                $"| {Name,25} | {KeyType,13} | {KeyDataType,9} | {IsOrdered,8} |{IsFullTextIndexed,8} |";
        }


        public KeyValue Value(object value)
        {
            return ValueToKeyValue(value, this);
        }

        /// <summary>
        ///     Helper method. Convert a value to a key value
        /// </summary>
        /// <param name="value"> </param>
        /// <param name="info"> </param>
        /// <returns> </returns>
        public static KeyValue ValueToKeyValue(object value, KeyInfo info)
        {
            //check if directly assignable to int
            if (info.KeyDataType == KeyDataType.IntKey || info.KeyDataType == KeyDataType.Default)
            {
                // default behavior for nullable values (works fine for dates)
                if (value == null)
                    return new KeyValue(0, info);

                var propertyType = value.GetType();

                //integer types
                if (propertyType == typeof(int) || propertyType == typeof(short) || propertyType == typeof(long) ||
                    propertyType == typeof(byte) || propertyType == typeof(char) || propertyType == typeof(bool) ||
                    propertyType == typeof(IntPtr))
                {
                    var longVal = Convert.ToInt64(value);
                    return new KeyValue(longVal, info);
                }

                if (propertyType.IsEnum)
                {
                    var longVal = Convert.ToInt64(value);
                    return new KeyValue(longVal, info);
                }


                //other types. Can be used as keys if a key converter is provided
                var converter = KeyConverters.GetIfAvailable(propertyType);
                if (converter != null)
                {
                    //prefer conversion to long if available
                    if (converter.CanConvertToLong) return new KeyValue(converter.GetAsLong(value), info);

                    if (converter.CanConvertToString) return new KeyValue(converter.GetAsString(value), info);

                    Dbg.CheckThat(false, "trying to use an invalid key converter");
                }

                if (info.KeyDataType == KeyDataType.Default)
                {
                    return new KeyValue(value.ToString(), info);
                }
            }
            else
            {
                if (value != null)
                    return new KeyValue(value.ToString(), info);
                return new KeyValue(null, info);
            }


            throw new InvalidOperationException($"Can not compute key value for object {value}");
        }
    }
}