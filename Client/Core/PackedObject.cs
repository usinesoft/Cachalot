using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Client.Messages;
using Client.Tools;
using ICSharpCode.SharpZipLib.GZip;
using JetBrains.Annotations;

using ProtoBuf;
// ReSharper disable PropertyCanBeMadeInitOnly.Local

namespace Client.Core;

/// <summary>
///     Any object is converted to this form while stored in the database and transferred through the
///     network. Only server-side values are available for queries, the rest of the object is serialized as json
/// </summary>
[ProtoContract]
public sealed class PackedObject
{
    /// <summary>
    ///     Default constructor for serialization only
    /// </summary>
    // ReSharper disable once UnusedMember.Local
    private PackedObject()
    {
    }

    /// <summary>
    ///     This property is not persistent. It is used when ordering items from multiple nodes.
    /// </summary>
    public double Rank { get; set; }

    /// <summary>
    ///     The one and only primary key
    /// </summary>
    public KeyValue PrimaryKey => Values[0];


    /// <summary>
    ///     Full name of the defining type
    /// </summary>
    [field: ProtoMember(1)]
    public string CollectionName { get; private set; }


    /// <summary>
    ///     The original object serialized to byte[]
    /// </summary>
    [field: ProtoMember(2)]
    public byte[] ObjectData { get; private set; }

    [field: ProtoMember(3)] public Layout Layout { get; private set; }

    /// <summary>
    ///     Keys that can be used for "Contains" queries
    /// </summary>
    [field: ProtoMember(4)]
    public KeyValues[] CollectionValues { get; private set; }

    [field: ProtoMember(5)] public string[] FullText { get; private set; }

    /// <summary>
    ///     Store tokenized full text to avoid tokenization time while loading database from storage
    /// </summary>
    [field: ProtoMember(6)]
    public IList<TokenizedLine> TokenizedFullText { get; set; }

    [field: ProtoMember(7)] public KeyValue[] Values { get; private set; }


    public string GlobalKey => CollectionName + PrimaryKey;
    
    public KeyValue this[int order] => Values[order];


    public static PackedObject Pack<TObject>(TObject instance, [NotNull] CollectionSchema typeDescription,
                                             string collectionName = null)
    {
        if (instance == null) throw new ArgumentNullException(nameof(instance));
        if (typeDescription == null) throw new ArgumentNullException(nameof(typeDescription));


        var result = new PackedObject
        {
            // scalar values that are visible server-side
            Values = new KeyValue[typeDescription.ServerSide.Count(k => !k.IsCollection)],
            // vector values that are visible server-side
            CollectionValues = new KeyValues[typeDescription.ServerSide.Count(k => k.IsCollection)]
        };


        // process server-side values
        var pos = 0;
        var collectionPos = 0;
        foreach (var metadata in typeDescription.ServerSide)
        {
            var getter = ExpressionTreeHelper.Getter<TObject>(metadata.Name);
            var value = getter(instance);

            if (!metadata.IsCollection)
            {
                // if the primary key is an empty Guid generate a value
                if (metadata.IndexType == IndexType.Primary && Guid.Empty.Equals(value)) value = Guid.NewGuid();

                result.Values[pos++] = new(value);
            }
            else
            {
                if (value is IEnumerable values and not string) // sting is enumerable but not a collection
                {
                    if (metadata.IndexType == IndexType.Ordered)
                        throw new NotSupportedException(
                            $"The property {metadata.Name} is a collection. It can be indexed as a dictionary but not as an ordered index");

                    var keyValues = values.Cast<object>().Select(v => new KeyValue(v));
                    result.CollectionValues[collectionPos++] = new(metadata.Name, keyValues);
                }
                else
                {
                    throw new NotSupportedException(
                        $"The property {metadata.Name} is declared as a collection in the schema but the value is not a collection: {value.GetType().FullName}");
                }
            }
        }


        // process full text
        var lines = new List<string>();

        foreach (var fulltext in typeDescription.FullText)
            lines.AddRange(ExpressionTreeHelper.GetStringValues(instance, fulltext));


        result.FullText = lines.ToArray();


        // for "flat" objects do not store data. Everything is in the key-values
        if (typeDescription.StorageLayout != Layout.Flat)
            result.ObjectData =
                SerializationHelper.ObjectToBytes(instance, SerializationMode.Json,
                    typeDescription.StorageLayout == Layout.Compressed);

        result.CollectionName = collectionName ?? typeDescription.CollectionName;

        result.Layout = typeDescription.StorageLayout;


        return result;
    }

    public string GetJson(CollectionSchema schema)
    {
        if (Layout == Layout.Flat) return ToJsonObjectForFlatLayout(schema).ToString();
        
        var stream = new MemoryStream(ObjectData);
        
        if (Layout != Layout.Compressed) return new StreamReader(stream).ReadToEnd();
        
        var zInStream = new GZipInputStream(stream);
        return new StreamReader(zInStream).ReadToEnd();
        
    }

    

    private JsonObject ToJsonObject(int[] valuesOrder, string[] valueNames)
    {
        var result = new JsonObject();

        var index = 0;
        foreach (var i in valuesOrder)
        {
            if (i < Values.Length) // scalar value
            {
                var kv = Values[i];
                var jp = kv.ToJsonValue();


                result.Add(valueNames[index], jp);
            }
            else // collections
            {
                var collectionIndex = i - Values.Length;

                var collection = CollectionValues[collectionIndex];

                var array = new JsonArray();
                foreach (var keyValue in collection.Values)
                {
                    array.Add(keyValue.ToJsonValue());
                }

                result.Add(collection.Name, array);
            }

            index++;
        }

        return result;
    }

    public JsonObject ToJsonObjectForFlatLayout(CollectionSchema schema)
    {
        var result = new JsonObject();

        var index = 0;
        foreach (var kv in Values)
        {
            var name = schema.ServerSide[index].Name;
            
            var jv = kv.ToJsonValue();

            result.Add(name, jv);

            index++;
        }

        return result;
    }


    /// <summary>
    ///     Return object data. It may be complete data or a projection if <see cref="valuesOrder" /> contains any element
    /// </summary>
    /// <param name="valuesOrder">indexes of values to be serialized</param>
    /// <param name="valueNames">names (may be the original names in the schema definition or alias names)</param>
    /// <returns></returns>
    public byte[] GetData(int[] valuesOrder, string[] valueNames)
    {
        if (valuesOrder.Length > 0)
            return SerializationHelper.ObjectToBytes(ToJsonObject(valuesOrder, valueNames), SerializationMode.Json,
                Layout == Layout.Compressed);

        return ObjectData;
    }

    

    public static PackedObject PackJson(string json, CollectionSchema collectionSchema)
    {
        var jDoc = JsonDocument.Parse(json);
        
        return PackJson(jDoc, collectionSchema);
    }

    public static PackedObject PackJson(JsonDocument jObject, CollectionSchema collectionSchema,
                                        string collectionName = null)
    {
        
        var result = new PackedObject
        {
            // scalar values that are visible server-side
            Values = new KeyValue[collectionSchema.ServerSide.Count(k => !k.IsCollection)],
            // vector values that are visible server-side
            CollectionValues = new KeyValues[collectionSchema.ServerSide.Count(k => k.IsCollection)]
        };

        // process server-side values
        var pos = 0;
        var collectionPos = 0;
        foreach (var metadata in collectionSchema.ServerSide)
        {
            if (!jObject.RootElement.TryGetProperty(metadata.JsonName, out var jKey) && !metadata.IsCollection)
            {
                // as default values are ignored in json set 0 if primary key, null otherwise
                result.Values[pos++] = metadata.IndexType == IndexType.Primary?new KeyValue(0): new KeyValue(null);
                continue;
            }

            if (!metadata.IsCollection)
            {
                result.Values[pos++] = jObject.RootElement.JsonPropertyToKeyValue(metadata);
            }
            else
            {
                if (jKey.ValueKind == JsonValueKind.Array)
                {
                    if (metadata.IndexType == IndexType.Ordered)
                        throw new NotSupportedException(
                            $"The property {metadata.Name} is a collection. It can be indexed as a dictionary but not as an ordered index");

                    var keyValues = jKey.JsonPropertyToKeyValues(metadata);

                    result.CollectionValues[collectionPos++] = keyValues;
                }
                else if (jKey.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) // create an empty collection if no data
                {
                    result.CollectionValues[collectionPos++] = new(metadata.Name, Enumerable.Empty<KeyValue>());
                }
                else
                {
                    throw new NotSupportedException(
                        $"The property {metadata.Name} is declared as a collection in the schema but the value is not a collection: {metadata.JsonName} ");
                }
            }
        }


        // process full text
        var lines = new List<string>();

        foreach (var fulltext in collectionSchema.FullText)
        {
            if(!jObject.RootElement.TryGetProperty(fulltext, out var jKey))
            {
                continue;
            }

            jKey.ExtractTextFromJsonElement(lines);
            
        }

        result.FullText = lines.ToArray();

         
        if (collectionSchema.StorageLayout != Layout.Flat)
            result.ObjectData = SerializationHelper.ObjectToBytes(jObject, SerializationMode.Json,
                collectionSchema.StorageLayout == Layout.Compressed);


        result.CollectionName = collectionName ?? collectionSchema.CollectionName;

        result.Layout = collectionSchema.StorageLayout;


        return result;
    }

    /// <summary>
    ///     Pack a csv line. Assume the flat layout in this case. All fields are scalars and are server-side visible
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public static PackedObject PackCsv(int primaryKey, string line, string collectionName, CsvSchema csvSchema)
    {
        var values = csvSchema.ParseLine(line);

        var result = new PackedObject
        {
            Values = new KeyValue[values.Count + 1], // +1 for the primary key

            CollectionValues = Array.Empty<KeyValues>()
        };

        // set the primary key
        result.Values[0] = new(primaryKey);


        // process server-side values
        var pos = 1;
        foreach (var value in values)
        {
            result.Values[pos] = value;

            pos++;
        }


        // for now no full text search for flat layout
        result.FullText = Array.Empty<string>();


        result.CollectionName = collectionName;

        result.Layout = Layout.Flat;


        return result;
    }


    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(PrimaryKey);
        sb.AppendLine(" {");


        if (Values is { Length: > 0 })
        {
            sb.Append("  values:");
            foreach (var key in Values)
            {
                sb.Append(key);
                sb.Append(" ");
            }

            sb.AppendLine();
        }

        if (FullText is { Length: > 0 })
        {
            sb.AppendLine("  full text:");
            foreach (var key in FullText) sb.AppendLine($"    {key}");
            sb.AppendLine();
        }

        sb.AppendLine(" } ");

        if (ObjectData != null)
            sb.AppendLine($"{ObjectData.Length} bytes");

        return sb.ToString();
    }


    /// <summary>
    ///     Restore the original object
    /// </summary>
    /// <param name="packedObject"> </param>
    /// <param name="schema">
    ///     schema is required only for objects with <see cref="Core.Layout.Flat" /> layout as they do not
    ///     contain json data
    /// </param>
    /// <returns> </returns>
    public static T Unpack<T>(PackedObject packedObject, CollectionSchema schema)
    {
        if (packedObject.Layout == Layout.Flat)
        {
            var json = packedObject.GetJson(schema);
            return SerializationHelper.ObjectFromCompactJson<T>(json);
        }

        return SerializationHelper.ObjectFromBytes<T>(packedObject.ObjectData, SerializationMode.Json,
            packedObject.Layout == Layout.Compressed);
    }

    /// <summary>
    ///     Repack an object with a new schema. Old schema is required only for flat layout as the json is not stored in the
    ///     object, and it has to be regenerated
    /// </summary>
    /// <param name="packedObject"></param>
    /// <param name="oldSchema"></param>
    /// <param name="newSchema"></param>
    /// <returns></returns>
    public static PackedObject Repack(PackedObject packedObject, CollectionSchema oldSchema, CollectionSchema newSchema)
    {
        var json = packedObject.GetJson(oldSchema);
        return PackJson(json, newSchema);
    }

    

    public KeyValues Collection(int order)
    {
        return CollectionValues[order];
    }
}