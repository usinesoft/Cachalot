#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Client.Messages;
using ICSharpCode.SharpZipLib.GZip;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using ProtoBuf;

#endregion

namespace Client.Core
{
    public static class SerializationHelper
    {
        /// <summary>
        ///     Create a serializer that produces human readable json
        /// </summary>
        public static JsonSerializer FormattedSerializer
        {
            get
            {
                var serializer = JsonSerializer.Create(new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    TypeNameHandling = TypeNameHandling.None
                });
                serializer.Converters.Add(new StringEnumConverter());

                return serializer;
            }
        }


        public static JsonSerializerSettings JsonSettings(TypeDescription typeDescription = null)
        {
            return new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects,
                //DateFormatString = "yyyy-MM-dd",
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore

                //ContractResolver = new WritablePropertiesOnlyResolver(typeDescription)
            };
        }


        public static TItem ObjectFromStream<TItem>(Stream stream, SerializationMode mode, bool compress)

        {
            if (mode == SerializationMode.Json)
            {
                BinaryReader reader;
                if (compress)
                {
                    var zInStream = new GZipInputStream(stream);
                    reader = new BinaryReader(zInStream);
                    return JsonConvert.DeserializeObject<TItem>(reader.ReadString(), JsonSettings());
                }

                reader = new BinaryReader(stream);
                return JsonConvert.DeserializeObject<TItem>(reader.ReadString(), JsonSettings());
            }

            return Serializer.DeserializeWithLengthPrefix<TItem>(stream, PrefixStyle.Fixed32);
        }


        public static string AsJson(this CachedObject obj)
        {
            var stream = new MemoryStream(obj.ObjectData);

            if (obj.UseCompression)
                using (var zInStream = new GZipInputStream(stream))
                {
                    var reader = new BinaryReader(zInStream);
                    var json = reader.ReadString();
                    return JToken.Parse(json).ToString(Formatting.Indented);
                }

            {
                var reader = new BinaryReader(stream);
                var json = reader.ReadString();
                return JToken.Parse(json).ToString(Formatting.Indented);
            }
        }

        public static TItem ObjectFromBytes<TItem>(byte[] bytes, SerializationMode mode, bool compress)
        {
            var stream = new MemoryStream(bytes);


            if (mode == SerializationMode.Json)
            {
                string json;

                if (compress)
                {
                    using (var zInStream = new GZipInputStream(stream))
                    {
                        var reader = new BinaryReader(zInStream);
                        json = reader.ReadString();
                    }
                }
                else
                {
                    var reader = new BinaryReader(stream);
                    json = reader.ReadString();
                }


                return JsonConvert.DeserializeObject<TItem>(json, JsonSettings());
            }

            return Serializer.DeserializeWithLengthPrefix<TItem>(stream, PrefixStyle.Fixed32);
        }

        public static void ObjectToStream<TItem>(TItem obj, Stream stream, SerializationMode mode, bool compress)
        {
            if (mode == SerializationMode.Json)
            {
                var json = JsonConvert.SerializeObject(obj, JsonSettings());

                if (compress)
                {
                    var outZStream = new GZipOutputStream(stream);
                    var writer = new BinaryWriter(outZStream);
                    writer.Write(json);

                    outZStream.Flush();
                    outZStream.Finish();
                }
                else
                {
                    var writer = new BinaryWriter(stream);
                    writer.Write(json);
                }
            }
            else
            {
                Serializer.SerializeWithLengthPrefix(stream, obj, PrefixStyle.Fixed32);
            }
        }

        public static byte[] ObjectToBytes<TItem>(TItem obj, SerializationMode mode, TypeDescription typeDescription)
        {
            using (var output = new MemoryStream())
            {
                if (mode == SerializationMode.Json)
                {
                    var json = ObjectToJson(obj, typeDescription);

                    if (typeDescription.UseCompression)
                    {
                        using (var outZStream = new GZipOutputStream(output))
                        {
                            var writer = new BinaryWriter(outZStream);
                            writer.Write(json);
                            outZStream.Flush();
                            outZStream.Finish();
                        }
                    }
                    else
                    {
                        var writer = new BinaryWriter(output);
                        writer.Write(json);
                    }
                }
                else
                {
                    Serializer.SerializeWithLengthPrefix(output, obj, PrefixStyle.Fixed32);
                }


                return output.ToArray();
            }
        }


        /// <summary>
        ///     Serialize an object to compact json. For storage not for human readablility
        ///     Read only fields are ignored unless they are indexes, the output is not indented, type information is stored
        ///     to allow deserializing polymorphic collections
        /// </summary>
        /// <typeparam name="TItem"></typeparam>
        /// <param name="obj"></param>
        /// <param name="typeDescription"></param>
        /// <returns></returns>
        public static string ObjectToJson<TItem>(TItem obj, TypeDescription typeDescription)
        {
            return JsonConvert.SerializeObject(obj, JsonSettings(typeDescription));
        }

        /// <summary>
        ///     Serialize an object as human readable json. Enums are converted to string. output is indented
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item"></param>
        /// <returns></returns>
        public static string SerializeFormatedJson<T>(T item)
        {
            var sb = new StringBuilder();

            FormattedSerializer.Serialize(new JsonTextWriter(new StringWriter(sb)), item);

            return sb.ToString();
        }

        public static T DeserializeJson<T>(string json)
        {
            return FormattedSerializer.Deserialize<T>(new JsonTextReader(new StringReader(json)));
        }

        /// <summary>
        ///     Properties resolver for the Json serializer
        ///     It selects only writable proparties and readonly properties that are indexed
        /// </summary>
        private class WritablePropertiesOnlyResolver : DefaultContractResolver
        {
            private readonly Dictionary<Type, List<JsonProperty>> _propertiesCache =
                new Dictionary<Type, List<JsonProperty>>();

            private readonly TypeDescription _tyeDescription;


            public WritablePropertiesOnlyResolver(TypeDescription tyeDescription)
            {
                _tyeDescription = tyeDescription;
            }

            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                lock (_propertiesCache)
                {
                    var props = base.CreateProperties(type, memberSerialization);

                    if (_propertiesCache.TryGetValue(type, out var properties)) return properties;

                    properties = props.Where(p =>
                        p.Writable || _tyeDescription != null && _tyeDescription.IsIndexed(p.PropertyName)).ToList();
                    _propertiesCache[type] = properties;

                    return properties;
                }
            }
        }
    }
}