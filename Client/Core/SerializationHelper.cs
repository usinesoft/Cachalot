#region

using System.IO;
using System.Text;
using Client.Messages;
using ICSharpCode.SharpZipLib.GZip;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using ProtoBuf;

#endregion

namespace Client.Core
{
    public static class SerializationHelper
    {
        public static readonly JsonSerializer Serializer = JsonSerializer.Create(JsonSettings());

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


        private static JsonSerializerSettings JsonSettings()
        {
            return new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects,
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore,
                
            };
        }


        public static TItem ObjectFromStream<TItem>(Stream stream, SerializationMode mode, bool compress)

        {
            if (mode != SerializationMode.Json)
                return ProtoBuf.Serializer.DeserializeWithLengthPrefix<TItem>(stream, PrefixStyle.Fixed32);


            JsonTextReader reader;
            if (compress)
            {
                var zInStream = new GZipInputStream(stream);
                reader = new JsonTextReader(new StreamReader(zInStream));

                
                return Serializer.Deserialize<TItem>(reader);
            }


            reader = new JsonTextReader(new StreamReader(stream));
            return Serializer.Deserialize<TItem>(reader);
        }


        public static string AsJson(this CachedObject obj)
        {
            if (obj.UseCompression)
            {
                var stream = new MemoryStream(obj.ObjectData);
                using (var zInStream = new GZipInputStream(stream))
                {
                    var stream2 = new MemoryStream();
                    zInStream.CopyTo(stream2);

                    var json = Encoding.UTF8.GetString(stream2.ToArray());

                    return JToken.Parse(json).ToString(Formatting.Indented);
                }
            }

            {
                var json = Encoding.UTF8.GetString(obj.ObjectData);

                return JToken.Parse(json).ToString(Formatting.Indented);
            }
        }

        public static TItem ObjectFromBytes<TItem>(byte[] bytes, SerializationMode mode, bool compress)
        {
            var stream = new MemoryStream(bytes);


            if (mode == SerializationMode.Json) return ObjectFromStream<TItem>(stream, mode, compress);

            return ProtoBuf.Serializer.DeserializeWithLengthPrefix<TItem>(stream, PrefixStyle.Fixed32);
        }

        public static void ObjectToStream<TItem>(TItem obj, Stream stream, SerializationMode mode, bool compress)
        {
            if (mode == SerializationMode.Json)
            {
                if (compress)
                {
                    using (var outZStream = new GZipOutputStream(stream) {IsStreamOwner = false})
                    {
                        var writer = new JsonTextWriter(new StreamWriter(outZStream));
                        Serializer.Serialize(writer, obj);
                        writer.Flush();
                    }
                }
                else
                {
                    var writer = new JsonTextWriter(new StreamWriter(stream));
                    Serializer.Serialize(writer, obj);
                    writer.Flush();
                }
            }
            else
            {
                ProtoBuf.Serializer.SerializeWithLengthPrefix(stream, obj, PrefixStyle.Fixed32);
            }
        }

        public static byte[] ObjectToBytes<TItem>(TItem obj, SerializationMode mode, bool useCompression)
        {
            using (var output = new MemoryStream())
            {
                if (mode == SerializationMode.Json)
                    ObjectToStream(obj, output, mode, useCompression);
                else
                    ProtoBuf.Serializer.SerializeWithLengthPrefix(output, obj, PrefixStyle.Fixed32);

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
        /// <returns></returns>
        public static string ObjectToJson<TItem>(TItem obj)
        {
            return JsonConvert.SerializeObject(obj, JsonSettings());
        }


        public static T DeserializeJson<T>(string json)
        {
            return FormattedSerializer.Deserialize<T>(new JsonTextReader(new StringReader(json)));
        }
    }
}