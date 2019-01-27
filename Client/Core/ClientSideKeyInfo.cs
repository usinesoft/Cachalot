#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Client.Interface;
using Client.Messages;

#endregion

namespace Client.Core
{
    /// <summary>
    ///     Represents the metadata associated with a key (PropertyInfo + KeyType + KeyDataType)
    ///     It can be used to produce a <see cref="KeyValue" />
    ///     It can be converted to <see cref="KeyInfo" /> for serialization
    /// </summary>
    public class ClientSideKeyInfo : IEquatable<ClientSideKeyInfo>
    {
        /// <summary>
        ///     if true order operators can be used for this key
        /// </summary>
        private readonly bool _isOrdered;

        /// <summary>
        ///     Any key type must be convertible to LongInt or String
        /// </summary>
        private readonly KeyDataType _keyDataType;

        /// <summary>
        ///     description of the underlying property
        /// </summary>
        private readonly PropertyInfo _propertyInfo;

        /// <summary>
        ///     Build from PropertyInfo
        ///     The complementary information is stored as custom attributes
        /// </summary>
        /// <param name="propertyInfo"> </param>
        public ClientSideKeyInfo(PropertyInfo propertyInfo)
        {
            _propertyInfo = propertyInfo;

            //check if primary key
            var attributes = propertyInfo.GetCustomAttributes(typeof(PrimaryKeyAttribute), true);
            if (attributes.Length == 1)
            {
                KeyType = KeyType.Primary;
                if (attributes[0] is PrimaryKeyAttribute attr)
                    _keyDataType = attr.KeyDataType;

                return;
            }

            //check if unique key
            attributes = propertyInfo.GetCustomAttributes(typeof(KeyAttribute), true);
            if (attributes.Length == 1)
            {
                KeyType = KeyType.Unique;
                if (attributes[0] is KeyAttribute attr)
                    _keyDataType = attr.KeyDataType;

                return;
            }

            //check if index
            attributes = propertyInfo.GetCustomAttributes(typeof(IndexAttribute), true);
            if (attributes.Length > 0)
            {
                // Index attribute may be applied to scalar types or collections

                if (propertyInfo.PropertyType == typeof(string))
                    // the string is IEnumerable but it should be treated as scalar
                    KeyType = KeyType.ScalarIndex;
                else
                    KeyType = typeof(IEnumerable).IsAssignableFrom(propertyInfo.PropertyType)
                        ? KeyType.ListIndex
                        : KeyType.ScalarIndex;


                if (attributes[0] is IndexAttribute attr)
                {
                    _keyDataType = attr.KeyDataType;
                    _isOrdered = attr.Ordered;
                }

                return;
            }

            KeyType = KeyType.None;
        }

        /// <summary>
        ///     Build from PropertyInfo
        ///     The complementary information is stored as <see cref="PropertyDescription" />
        /// </summary>
        /// <param name="propertyInfo"> </param>
        /// <param name="propertyDescription"> contains indexing information for this property </param>
        public ClientSideKeyInfo(PropertyInfo propertyInfo, PropertyDescription propertyDescription)
        {
            if (propertyDescription == null)
                throw new ArgumentNullException(nameof(propertyDescription));

            _propertyInfo = propertyInfo;


            KeyType = propertyDescription.KeyType;
            _keyDataType = propertyDescription.KeyDataType;
            _isOrdered = propertyDescription.Ordered;
        }

        /// <summary>
        ///     Return a serializable, light version <see cref="KeyInfo" />
        /// </summary>
        public KeyInfo AsKeyInfo => new KeyInfo(_keyDataType, KeyType, _propertyInfo.Name, _isOrdered);

        /// <summary>
        ///     int or string
        /// </summary>
        public KeyType KeyType { get; }

        /// <summary>
        ///     Any key type must be convertible to LongInt or String
        /// </summary>
        public KeyDataType KeyDataType => _keyDataType;

        /// <summary>
        ///     Name of the key (unique for a cacheable type)
        /// </summary>
        public string Name => _propertyInfo.Name;

        /// <summary>
        ///     if true order operators can be used for this key
        /// </summary>
        public bool IsOrdered => _isOrdered;

        /// <summary>
        ///     Equals from <see cref="IEquatable{T}" />
        ///     This type has a value type semantics
        /// </summary>
        /// <param name="keyInfo"> </param>
        /// <returns> </returns>
        public bool Equals(ClientSideKeyInfo keyInfo)
        {
            if (keyInfo == null)
                return false;
            if (!Equals(_propertyInfo, keyInfo._propertyInfo))
                return false;
            if (!Equals(KeyType, keyInfo.KeyType))
                return false;
            if (!Equals(_keyDataType, keyInfo._keyDataType))
                return false;

            return true;
        }

        /// <summary>
        /// </summary>
        /// <param name="keyInfo1"> </param>
        /// <param name="keyInfo2"> </param>
        /// <returns> </returns>
        public static bool operator !=(ClientSideKeyInfo keyInfo1, ClientSideKeyInfo keyInfo2)
        {
            return !Equals(keyInfo1, keyInfo2);
        }

        /// <summary>
        /// </summary>
        /// <param name="keyInfo1"> </param>
        /// <param name="keyInfo2"> </param>
        /// <returns> </returns>
        public static bool operator ==(ClientSideKeyInfo keyInfo1, ClientSideKeyInfo keyInfo2)
        {
            return Equals(keyInfo1, keyInfo2);
        }

        /// <summary>
        ///     Equals from <see cref="object" />
        /// </summary>
        /// <param name="obj"> </param>
        /// <returns> </returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            return Equals(obj as ClientSideKeyInfo);
        }

        public override int GetHashCode()
        {
            var result = _propertyInfo.GetHashCode();
            result = 29 * result + KeyType.GetHashCode();
            result = 29 * result + _keyDataType.GetHashCode();
            return result;
        }

        /// <summary>
        ///     Create a <see cref="KeyValue" />. Ensures conversion from all integer types and <see cref="DateTime" /> to long int
        ///     The default DateTime conversion ignores the time
        /// </summary>
        /// <param name="instance"> the object containing the key </param>
        /// <returns>
        ///     value encoded as <see cref="KeyValue" />
        /// </returns>
        public KeyValue GetValue(object instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var value = _propertyInfo.GetValue(instance, null);

            return KeyInfo.ValueToKeyValue(value, AsKeyInfo);
        }

        /// <summary>
        ///     Get the values in a an IEnumerable property
        /// </summary>
        /// <param name="instance"></param>
        /// <returns></returns>
        public IEnumerable<KeyValue> GetCollectionValues(object instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            if (!(_propertyInfo.GetValue(instance, null) is IEnumerable values))
                throw new NotSupportedException($"Property {_propertyInfo.Name} can not be converted to IEnumerable");

            return (from object value in values select KeyInfo.ValueToKeyValue(value, AsKeyInfo)).ToList();
        }
    }
}