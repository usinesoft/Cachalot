#region

using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ICSharpCode.SharpZipLib.GZip;
using ProtoBuf;
using JsonSerializer = System.Text.Json.JsonSerializer;

#endregion

namespace Client.Core;

public static class SerializationHelper
{

    /// <summary>
    /// To serialize human readable Json
    /// </summary>
    static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
    };

    /// <summary>
    /// To serialize compact Json
    /// </summary>
    static readonly JsonSerializerOptions CompactOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new SmartDateTimeConverter(), new SmartDateTimeOffsetConverter() }
    };

    public static TItem ObjectFromStream<TItem>(Stream stream, SerializationMode mode, bool compress)

    {
        if (mode != SerializationMode.Json)
            return Serializer.DeserializeWithLengthPrefix<TItem>(stream, PrefixStyle.Base128);


        if (compress)
        {
            var zInStream = new GZipInputStream(stream);

            return JsonSerializer.Deserialize<TItem>(zInStream, CompactOptions);
        }

        return JsonSerializer.Deserialize<TItem>(stream, CompactOptions);
    }


    public static JsonDocument JsonDocumentFromStream(Stream stream, bool compress)
    {
        if (compress)
        {
            var zInStream = new GZipInputStream(stream);

            return JsonDocument.Parse(zInStream);
        }

        return JsonDocument.Parse(stream);
    }


    public static string AsJson(this PackedObject obj, CollectionSchema schema)
    {
        switch (obj.Layout)
        {
            case Layout.Compressed:
            {
                var stream = new MemoryStream(obj.ObjectData);
                using var zInStream = new GZipInputStream(stream);

                var stream2 = new MemoryStream();
                zInStream.CopyTo(stream2);

                var json = Encoding.UTF8.GetString(stream2.ToArray());

                return json;
            }
            case Layout.Default:
            {
                var json = Encoding.UTF8.GetString(obj.ObjectData);

                return json;
            }
            default: // flat layout: we need to build the json
                return obj.GetJson(schema);
        }
    }

    public static TItem ObjectFromBytes<TItem>(byte[] bytes, SerializationMode mode, bool compress)
    {
        var stream = new MemoryStream(bytes);


        if (mode == SerializationMode.Json) return ObjectFromStream<TItem>(stream, mode, compress);

        return Serializer.DeserializeWithLengthPrefix<TItem>(stream, PrefixStyle.Base128);
    }

    public static void ObjectToStream<TItem>(TItem obj, Stream stream, SerializationMode mode, bool compress)
    {
        if (mode == SerializationMode.Json)
        {
            if (compress)
            {
                using var outZStream = new GZipOutputStream(stream) { IsStreamOwner = false };

                JsonSerializer.Serialize(outZStream, obj, CompactOptions);
            }
            else
            {
                JsonSerializer.Serialize(stream, obj, CompactOptions);
            }
        }
        else
        {
            Serializer.SerializeWithLengthPrefix(stream, obj, PrefixStyle.Base128);
        }
    }

    public static byte[] ObjectToBytes<TItem>(TItem obj, SerializationMode mode, bool useCompression)
    {
        using var output = new MemoryStream();
        if (mode == SerializationMode.Json)
            ObjectToStream(obj, output, mode, useCompression);
        else
            Serializer.SerializeWithLengthPrefix(output, obj, PrefixStyle.Base128);

        return output.ToArray();
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
        return JsonSerializer.Serialize(obj, Options);
    }

    public static TItem ObjectFromJson<TItem>(string json)
    {
        return JsonSerializer.Deserialize<TItem>(json, Options);
    }

    public static string ObjectToCompactJson<TItem>(TItem obj)
    {
        return JsonSerializer.Serialize(obj, CompactOptions);
    }

    public static TItem ObjectFromCompactJson<TItem>(string json)
    {
        return JsonSerializer.Deserialize<TItem>(json, CompactOptions);
    }


    public static T DeserializeJson<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, Options);
    }

    public static T DeserializeJson<T>(JsonDocument jDoc)
    {
        return jDoc.Deserialize<T>(Options);
        
    }
    
}