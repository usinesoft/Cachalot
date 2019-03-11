#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using Client.Interface;
using Client.Messages;
using Client.Tools;

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

        public bool IndexedAsFulltext { get; }

        private Func<object, object> Getter { get; }

        private readonly KeyInfo _keyInfoCache;
        

        /// <summary>
        ///     Build from PropertyInfo
        ///     The complementary information is stored as custom attributes
        /// </summary>
        /// <param name="propertyInfo"> </param>
        public ClientSideKeyInfo(PropertyInfo propertyInfo)
        {

            try
            {
                _propertyInfo = propertyInfo;

                Getter = _propertyInfo.CompileGetter();

                // full text indexation can be applied to any type of key or event to non indexed properties
                var fullText = propertyInfo.GetCustomAttributes(typeof(FullTextIndexationAttribute), true).FirstOrDefault();

                if (fullText != null)
                {
                    IndexedAsFulltext = true;
                }


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
            finally
            {
                _keyInfoCache = new KeyInfo(_keyDataType, KeyType, Info.Name, _isOrdered, IndexedAsFulltext);
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

            _propertyInfo = propertyInfo;

            Getter = _propertyInfo.CompileGetter();

            KeyType = propertyDescription.KeyType;
            _keyDataType = propertyDescription.KeyDataType;
            _isOrdered = propertyDescription.Ordered;
            IndexedAsFulltext = propertyDescription.FullTextIndexed;

            _keyInfoCache = new KeyInfo(_keyDataType, KeyType, Info.Name, _isOrdered, IndexedAsFulltext);
        }

        /// <summary>
        ///     Return a serializable, light version <see cref="KeyInfo" />
        /// </summary>
        public KeyInfo AsKeyInfo => _keyInfoCache;

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
        public string Name => Info.Name;

        /// <summary>
        ///     if true order operators can be used for this key
        /// </summary>
        public bool IsOrdered => _isOrdered;

        /// <summary>
        ///     description of the underlying property
        /// </summary>
        public PropertyInfo Info => _propertyInfo;

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
            var result = Info.GetHashCode();
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
            //TODO use precompiled accessors
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            //var value = Getter(instance);

            var value = Info.GetValue(instance, null);

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

            if (!(Info.GetValue(instance, null) is IEnumerable values))
                throw new NotSupportedException($"Property {Info.Name} can not be converted to IEnumerable");

            return (from object value in values select KeyInfo.ValueToKeyValue(value, AsKeyInfo)).ToList();
        }

        /// <summary>
        /// Used for full text indexation
        /// </summary>
        /// <param name="instance"></param>
        /// <returns></returns>
        public IList<string> GetStringValues(object instance)
        {

            List<string> result = new List<string>();

            if (instance == null)
                throw new ArgumentNullException(nameof(instance));


            if (Info.GetValue(instance, null) is string text) // string is also an IEnumerable but we do not want to be processed as a collection
            {
                result.Add(text);
            }
            else if (Info.GetValue(instance, null) is IEnumerable values)
            {
                foreach (var value in values)
                {
                    result.AddRange(ToStrings(value));
                }
            }
            else
            {
                var val = Info.GetValue(instance);
                if (val != null)
                {
                    result.AddRange(ToStrings(val));
                }
                
            }


            return result;
        }


        


        /// <summary>
        /// Used for full text indexation
        /// </summary>
        static readonly Dictionary<Type, List<Func<object, object>>> StringGetterCache = new Dictionary<Type, List<Func<object, object>>>();


        /// <summary>
        /// Generate precompiled getters for a type. This will avoid using reflection for each call
        /// </summary>
        /// <param name="type"></param>
        private static void GenerateAccessorsForType(Type type)
        {
            var accessors = new List<Func<object, object>>();
            foreach (var property in type.GetProperties())
            {
                if (property.PropertyType == typeof(string))
                {
                    accessors.Add(property.CompileGetter());                    
                }
            }

            StringGetterCache[type] = accessors;
        }


        /// <summary>
        /// Return a list off all the values of string properties
        /// </summary>
        /// <param name="instance"></param>
        /// <returns></returns>
        static IList<string> ToStrings(object instance)
        {

            var result = new List<string>();
            var type = instance.GetType();

            if (!StringGetterCache.ContainsKey(type))
            {
                GenerateAccessorsForType(type);
            }

            foreach (var accessor in StringGetterCache[type])
            {
                var val = accessor(instance);
                if (val != null)
                {
                    result.Add(val as string);
                }
                
            }

            return result;
        }
    }


 
}