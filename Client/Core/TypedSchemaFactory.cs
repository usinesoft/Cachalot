#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Client.Interface;
using Client.Messages;
using Newtonsoft.Json;

#endregion

namespace Client.Core
{
    /// <summary>
    /// factory class: builds a <see cref="CollectionSchema"/> from a dotnet type that is tagged with metadata
    /// </summary>
    public static class TypedSchemaFactory
    {
        
        /// <summary>
        ///     Not only a generic version. It also prepares the protobuf serializers which prevents race condition issues during
        ///     the lazy initialization of the serializer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static CollectionSchema FromType<T>()
        {
            return FromType(typeof(T));
        }

        
        /// <summary>
        /// Convert <see cref="PropertyInfo"/> to a serializable and language neutral format
        /// </summary>
        /// <param name="propertyInfo"></param>
        /// <returns></returns>
        private static KeyInfo BuildPropertyMetadata(PropertyInfo propertyInfo)
        {
            
            var name = propertyInfo.Name;
            
            string jsonName = null;
            
            // the name can be altered by a [JsonProperty] attribute
            var jsonAttribute = propertyInfo.GetCustomAttributes(typeof(JsonPropertyAttribute), true)
                .Cast<JsonPropertyAttribute>().FirstOrDefault();

            if (jsonAttribute != null)
            {
                jsonName = jsonAttribute.PropertyName;
            }

        
            //check if it is visible server-side
            var attribute = propertyInfo.GetCustomAttributes(typeof(ServerSideValueAttribute), true)
                .Cast<ServerSideValueAttribute>().FirstOrDefault();;
            if (attribute != null)
            {
                // force order of the primary key to 0
                var order = attribute.IndexType == IndexType.Primary ? 0 : attribute.LineNumber;

                // string implements IEnumerable but it is not a collection
                bool isCollection = typeof(IEnumerable).IsAssignableFrom(propertyInfo.PropertyType) &&
                                    propertyInfo.PropertyType != typeof(string);

              
                return new KeyInfo(name, order , attribute.IndexType, jsonName, isCollection);
            }


            return null;

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
        public static CollectionSchema FromType(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));


            var layout = Layout.Default;
            var storage = type.GetCustomAttributes(typeof(StorageAttribute), false).FirstOrDefault();
            if (storage != null)
            {
                var storageParams = (StorageAttribute) storage;
                layout = storageParams.StorageLayout;
            }

            var result = new CollectionSchema
            {
                StorageLayout = layout,
                CollectionName = type.Name
            };

            var props = type.GetProperties();

            foreach (var info in props)
            {
                var key = BuildPropertyMetadata(info);
                if (key != null)
                {
                    result.ServerSide.Add(key);
                }

                var fullText = info.GetCustomAttributes(typeof(FullTextIndexationAttribute), true)
                    .FirstOrDefault();

                if (fullText != null)
                {
                    result.FullText.Add(info.Name);
                }

            }



            // Adjust order. Line numbers give relative order of keys but they need to be adjusted to a continuous range with the primary key at index 0 
            
            result.ServerSide = result.ServerSide.OrderBy(x => x.Order).ToList();
            int scalarIndex = 0;
            int collectionIndex = 0;
            foreach (var item in result.ServerSide)
            {
                if (item.IsCollection)
                {
                    if(result.StorageLayout == Layout.Flat)
                    {
                        throw new NotSupportedException("A flat storage does not support indexed collections");
                    }
                    item.Order = collectionIndex++;
                }
                else // scalar property
                {
                    item.Order = scalarIndex++;
                }
            }
            
            

            //check if the newly registered type is valid
            if (result.PrimaryKeyField == null)
                throw new NotSupportedException($"No primary key defined for type {type}");
            

            return result;
        }

    }
}