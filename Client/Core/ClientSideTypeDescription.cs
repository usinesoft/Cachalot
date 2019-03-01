#region

using System;
using System.Collections.Generic;
using System.Linq;
using Client.Interface;
using Client.Messages;

#endregion

namespace Client.Core
{
    /// <summary>
    ///     Type description as registered on the client. The type description is needed to dynamically
    ///     extract key data from an object and avoids the use of reflexion each time an object needs
    ///     to be stored into the cache.
    /// </summary>
    public class ClientSideTypeDescription
    {
        private readonly List<ClientSideKeyInfo> _indexFields;
        private readonly List<ClientSideKeyInfo> _listFields;

        private readonly Dictionary<string, PropertyDescription> _propertyDescriptionByName =
            new Dictionary<string, PropertyDescription>();

        private readonly List<ClientSideKeyInfo> _uniqueKeyFields;

        /// <summary>
        ///     Private constructor (to prevent direct instantiation)
        ///     It can be instantiated by the factory method
        /// </summary>
        private ClientSideTypeDescription()
        {
            _uniqueKeyFields = new List<ClientSideKeyInfo>();
            _indexFields = new List<ClientSideKeyInfo>();
            _listFields = new List<ClientSideKeyInfo>();
            FullTextIndexed = new List<ClientSideKeyInfo>();
        }

        /// <summary>
        ///     Convert to a serializable form that can be used without any static dependency to
        ///     the original type
        /// </summary>
        public TypeDescription AsTypeDescription => new TypeDescription(this);

        /// <summary>
        ///     The one and only primary key
        /// </summary>
        public ClientSideKeyInfo PrimaryKeyField { get; private set; }

        /// <summary>
        ///     List of fields which are unique keys
        /// </summary>
        public IEnumerable<ClientSideKeyInfo> UniqueKeyFields => _uniqueKeyFields;

        /// <summary>
        ///     Number of unique fields
        /// </summary>
        public int UniqueKeysCount => _uniqueKeyFields.Count;

        /// <summary>
        ///     Number of index fields
        /// </summary>
        public int IndexCount => _indexFields.Count;

        /// <summary>
        ///     ist of fields which are indexed (non unique)
        /// </summary>
        public IEnumerable<ClientSideKeyInfo> IndexFields => _indexFields;

        public IEnumerable<ClientSideKeyInfo> ListFields => _listFields;

        /// <summary>
        ///     Fully qualified type name
        /// </summary>
        public string FullTypeName { get; private set; }

        /// <summary>
        ///     Short type name
        /// </summary>
        public string TypeName { get; private set; }


        public bool UseCompression { get; private set; }

        public List<ClientSideKeyInfo> FullTextIndexed { get; }


        /// <summary>
        ///     Not only a generic version. It also prepares the protobuf serializers which prevents race condition issues during
        ///     the lazy initialization of the serializer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static ClientSideTypeDescription RegisterType<T>()
        {
            return RegisterType(typeof(T));
        }

        /// <summary>
        ///     Factory method used to create a precompiled type description.
        ///     This version of the method uses a tagged type ( Attributes are attached to the public properties
        ///     which are indexed in the cache)
        ///     In order to be cacheable, a type must be serializable and must have exactly one primary key
        ///     Optionally it can have multiple unique keys and index keys
        /// </summary>
        /// <param name="type"> type to register (must be properly decorated) </param>
        /// <returns> not null type description if successful </returns>
        public static ClientSideTypeDescription RegisterType(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));


            var useCompression = false;
            var storage = type.GetCustomAttributes(typeof(StorageAttribute), false).FirstOrDefault();
            if (storage != null)
            {
                var storageParams = (StorageAttribute) storage;
                useCompression = storageParams.UseCompression;
            }

            var result = new ClientSideTypeDescription
            {
                UseCompression = useCompression,
                TypeName = type.Name,
                FullTypeName = type.FullName
            };

            var props = type.GetProperties();
            foreach (var info in props)
            {
                var key = new ClientSideKeyInfo(info);

                if(key.IndexedAsFulltext)
                    result.FullTextIndexed.Add(key);

                if (key.KeyType == KeyType.None)
                    continue;

                if (key.KeyType == KeyType.Primary)
                    result.PrimaryKeyField = key;
                else if (key.KeyType == KeyType.Unique)
                    result._uniqueKeyFields.Add(key);
                else if (key.KeyType == KeyType.ScalarIndex)
                    result._indexFields.Add(key);
                else if (key.KeyType == KeyType.ListIndex)
                    result._listFields.Add(key);


                var propertyDescription = new PropertyDescription
                {
                    PropertyName = key.Name,
                    KeyDataType = key.KeyDataType,
                    KeyType = key.KeyType,
                    Ordered = key.IsOrdered
                };

                result._propertyDescriptionByName[key.Name] = propertyDescription;
            }

            //check if the newly registered type is valid
            if (result.PrimaryKeyField == null)
                throw new NotSupportedException($"No primary key defined for type {type}");

            return result;
        }


        public PropertyDescription GetPropertyDescription(string name)
        {
            return _propertyDescriptionByName[name];
        }

        /// <summary>
        ///     Factory method used to create a precompiled type description.
        ///     This version of the method uses an external description (no need to attach attributes to the public properties)
        ///     In order to be cacheable, a type must be serializable and must have exactly one primary key
        ///     Optionally it can have multiple unique keys and index keys
        /// </summary>
        /// <param name="type"> </param>
        /// <param name="typeDescription"> </param>
        /// <returns> </returns>
        public static ClientSideTypeDescription RegisterType(Type type, TypeDescriptionConfig typeDescription)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (!type.IsSerializable)
                throw new NotSupportedException(
                    $"the type {type} is not serializable so it can not be registered as cacheable type");

            var result = new ClientSideTypeDescription
            {
                TypeName = type.Name,
                FullTypeName = type.FullName,
                UseCompression = typeDescription.UseCompression
            };

            var props = type.GetProperties();

            foreach (var info in props)
                if (typeDescription.Keys.ContainsKey(info.Name))
                {
                    var propertyDescription = typeDescription.Keys[info.Name];

                    result._propertyDescriptionByName[info.Name] = propertyDescription;

                    var key = new ClientSideKeyInfo(info, propertyDescription);

                    if (key.KeyType == KeyType.None)
                        continue;

                    if (key.KeyType == KeyType.Primary)
                        result.PrimaryKeyField = key;
                    else if (key.KeyType == KeyType.Unique)
                        result._uniqueKeyFields.Add(key);
                    else if (key.KeyType == KeyType.ScalarIndex)
                        result._indexFields.Add(key);
                    else if (key.KeyType == KeyType.ListIndex)
                        result._listFields.Add(key);
                }

            //check if the newly registered type is valid
            if (result.PrimaryKeyField == null)
                throw new NotSupportedException($"no primary key defined for type {type}");

            return result;
        }
    }
}