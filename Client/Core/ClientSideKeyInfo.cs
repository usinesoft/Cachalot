#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Client.Interface;
using Client.Messages;
using Client.Tools;
using JetBrains.Annotations;
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
        /// <summary>
        ///     Used for full text indexation
        /// </summary>
        private static readonly Dictionary<Type, List<Func<object, object>>> StringGetterCache =
            new Dictionary<Type, List<Func<object, object>>>();


        public bool IsServerSideVisible { get; } = false;

        /// <summary>
        ///     Build from PropertyInfo
        ///     The complementary information is stored as custom attributes
        /// </summary>
        /// <param name="propertyInfo"> </param>
        public ClientSideKeyInfo(PropertyInfo propertyInfo)
        {
            string name = propertyInfo.Name;

            try
            {
                Info = propertyInfo;

                
                // the name can be altered by a [JsonProperty] attribute
                var jsonAttribute = propertyInfo.GetCustomAttributes(typeof(JsonPropertyAttribute), true).Cast<JsonPropertyAttribute>().FirstOrDefault();
                if (jsonAttribute != null)
                {
                    name = jsonAttribute.PropertyName;
                }

                // full text indexation can be applied to any type of key or event to non indexed properties
                var fullText = propertyInfo.GetCustomAttributes(typeof(FullTextIndexationAttribute), true)
                    .FirstOrDefault();

                if (fullText != null) IndexedAsFulltext = true;

                //check if it is visible server-side
                var attributes = propertyInfo.GetCustomAttributes(typeof(ServerSideVisibleAttribute), true);
                if (attributes.Length == 1)
                {
                    IsServerSideVisible = true;
                }

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
                AsKeyInfo = new KeyInfo(KeyDataType, KeyType, name, IsOrdered, IndexedAsFulltext);
            }
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

            Info = propertyInfo;

            KeyType = propertyDescription.KeyType;
            KeyDataType = propertyDescription.KeyDataType;
            IsOrdered = propertyDescription.Ordered;
            IndexedAsFulltext = propertyDescription.FullTextIndexed;
            IsServerSideVisible = propertyDescription.ServerSideVisible; 

            AsKeyInfo = new KeyInfo(KeyDataType, KeyType, Info.Name, IsOrdered, IndexedAsFulltext);
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
            //TODO use precompiled accessors
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var value = Info.GetValue(instance, null);

            if (KeyDataType == KeyDataType.Generate)
            {
                if (value is Guid guid)
                {
                    if (guid == Guid.Empty)
                    {
                        value = Guid.NewGuid();
                    }
                }
                
            }

            return KeyInfo.ValueToKeyValue(value, AsKeyInfo);
        }

        public ServerSideValue GetServerValue(object instance)
        {
            //TODO use precompiled accessors
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var value = Info.GetValue(instance, null);

            var val = Convert.ToDecimal(value); 

            return new ServerSideValue{Name = Info.Name, Value = val};
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

            if (!(Info.GetValue(instance, null) is IEnumerable values))
                throw new NotSupportedException($"Property {Info.Name} can not be converted to IEnumerable");

            return (from object value in values select KeyInfo.ValueToKeyValue(value, AsKeyInfo)).ToList();
        }

        /// <summary>
        ///     Used for full text indexation
        /// </summary>
        /// <param name="instance"></param>
        /// <returns></returns>
        public IList<string> GetStringValues(object instance)
        {
            var result = new List<string>();

            if (instance == null)
                throw new ArgumentNullException(nameof(instance));


            if (Info.GetValue(instance, null) is string text
            ) // string is also an IEnumerable but we do not want to be processed as a collection
            {
                result.Add(text);
            }
            else if (Info.GetValue(instance, null) is IEnumerable values)
            {
                foreach (var value in values) result.AddRange(ToStrings(value));
            }
            else
            {
                var val = Info.GetValue(instance);
                if (val != null) result.AddRange(ToStrings(val));
            }


            return result;
        }


        /// <summary>
        ///     Generate precompiled getters for a type. This will avoid using reflection for each call
        /// </summary>
        /// <param name="type"></param>
        private static void GenerateAccessorsForType([NotNull] Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            var accessors = new List<Func<object, object>>();
            foreach (var property in type.GetProperties())
                if (property.PropertyType == typeof(string))
                    accessors.Add(property.CompileGetter());

            StringGetterCache[type] = accessors;
        }


        /// <summary>
        ///     Return a list off all the values of string properties
        /// </summary>
        /// <param name="instance"></param>
        /// <returns></returns>
        private static IList<string> ToStrings(object instance)
        {
            var result = new List<string>();
            var type = instance.GetType();

            if (type == typeof(string))
            {
                result.Add((string) instance);
            }
            else if (type.Namespace != null && type.Namespace.StartsWith("System"))
            {
                result.Add(instance.ToString());
            }
            else // some complex type
            {
                List<Func<object, object>> accessors;
                lock (StringGetterCache)
                {
                    if (!StringGetterCache.ContainsKey(type)) GenerateAccessorsForType(type);
                    accessors = StringGetterCache[type];
                }

                if (accessors != null)
                    foreach (var accessor in accessors)
                    {
                        var val = accessor(instance);
                        if (val != null) result.Add(val as string);
                    }
            }


            return result;
        }
    }
}