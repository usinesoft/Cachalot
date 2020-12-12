#region

using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Client.Interface;
using Client.Messages;
using Newtonsoft.Json;

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
        public bool IsServerSideVisible { get; }

        /// <summary>
        ///     Build from PropertyInfo
        ///     The complementary information is stored as custom attributes
        /// </summary>
        /// <param name="propertyInfo"> </param>
        public ClientSideKeyInfo(PropertyInfo propertyInfo)
        {
            var name = propertyInfo.Name;
            string jsonName = null;

            try
            {
                Info = propertyInfo;


                // the name can be altered by a [JsonProperty] attribute
                var jsonAttribute = propertyInfo.GetCustomAttributes(typeof(JsonPropertyAttribute), true)
                    .Cast<JsonPropertyAttribute>().FirstOrDefault();
                if (jsonAttribute != null) jsonName = jsonAttribute.PropertyName;

                // full text indexation can be applied to any type of key or event to non indexed properties
                var fullText = propertyInfo.GetCustomAttributes(typeof(FullTextIndexationAttribute), true)
                    .FirstOrDefault();

                if (fullText != null) IndexedAsFulltext = true;

                //check if it is visible server-side
                var attributes = propertyInfo.GetCustomAttributes(typeof(ServerSideVisibleAttribute), true);
                if (attributes.Length == 1) IsServerSideVisible = true;

                //check if primary key
                attributes = propertyInfo.GetCustomAttributes(typeof(PrimaryKeyAttribute), true);
                if (attributes.Length == 1)
                {
                    KeyType = KeyType.Primary;
                    if (attributes[0] is PrimaryKeyAttribute attr)
                        KeyDataType = attr.KeyDataType;


                    return;
                }

                //check if unique key
                attributes = propertyInfo.GetCustomAttributes(typeof(KeyAttribute), true);
                if (attributes.Length == 1)
                {
                    KeyType = KeyType.Unique;
                    if (attributes[0] is KeyAttribute attr)
                        KeyDataType = attr.KeyDataType;


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
                        KeyDataType = attr.KeyDataType;
                        IsOrdered = attr.Ordered;
                    }


                    return;
                }


                KeyType = KeyType.None;
            }
            finally
            {
                AsKeyInfo = new KeyInfo(KeyDataType, KeyType, name, IsOrdered, IndexedAsFulltext, IsServerSideVisible,
                    jsonName);
            }
        }


        public bool IndexedAsFulltext { get; }


        /// <summary>
        ///     Return a serializable, light version <see cref="KeyInfo" />
        /// </summary>
        public KeyInfo AsKeyInfo { get; }

        /// <summary>
        ///     int or string
        /// </summary>
        public KeyType KeyType { get; }

        /// <summary>
        ///     Any key type must be convertible to LongInt or String
        /// </summary>
        private KeyDataType KeyDataType { get; }

        /// <summary>
        ///     Name of the key (unique for a cacheable type)
        /// </summary>
        public string Name => Info.Name;

        /// <summary>
        ///     if true order operators can be used for this key
        /// </summary>
        private bool IsOrdered { get; }

        /// <summary>
        ///     description of the underlying property
        /// </summary>
        private PropertyInfo Info { get; }


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
            if (!Equals(Info, keyInfo.Info))
                return false;
            if (!Equals(KeyType, keyInfo.KeyType))
                return false;
            if (!Equals(KeyDataType, keyInfo.KeyDataType))
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
            var result = Info.GetHashCode();
            result = 29 * result + KeyType.GetHashCode();
            result = 29 * result + KeyDataType.GetHashCode();
            return result;
        }
    }
}