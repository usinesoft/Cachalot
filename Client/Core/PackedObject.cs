using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Client.Messages;
using ICSharpCode.SharpZipLib.GZip;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProtoBuf;

namespace Client.Core
{
    /// <summary>
    ///     Any object is converted to this form while stored in the database and transferred through the
    ///     network. Only server-side values are available, the rest of the object is serialized as json
    /// </summary>
    [ProtoContract]
    public class PackedObject
    {
        /// <summary>
        ///     This property is not persistent. It is used when ordering items from multiple nodes.
        /// </summary>
        public double Rank { get; set; }

        /// <summary>
        ///     The one and only primary key 
        /// </summary>
        public KeyValue PrimaryKey  => Values[0];


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

        [field: ProtoMember(3)] public bool UseCompression { get; private set; }

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

        /// <summary>
        ///     Default constructor for serialization only
        /// </summary>
        // ReSharper disable once UnusedMember.Local
        private PackedObject()
        {
        }

        

        public string GlobalKey => CollectionName + PrimaryKey;

       

        public static PackedObject Pack<TObject>(TObject instance, [NotNull] CollectionSchema typeDescription, string collectionName = null)
        {

            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (typeDescription == null) throw new ArgumentNullException(nameof(typeDescription));


            var result = new PackedObject
            {
                // scalar values that are visible server-side
                Values = new KeyValue[typeDescription.ServerSide.Count(k=>!k.IsCollection)],
                // vector values that are visible server-side
                CollectionValues = new KeyValues[typeDescription.ServerSide.Count(k=>k.IsCollection)]
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
                    if (metadata.IndexType == IndexType.Primary)
                    {
                        if (Guid.Empty.Equals(value))
                        {
                            value = Guid.NewGuid();
                        }
                    }

                    result.Values[pos++] = new KeyValue(value, metadata);
                }
                else 
                {
                    if (value is IEnumerable values && !(value is string))
                    {
                        if (metadata.IndexType == IndexType.Ordered)
                        {
                            throw new NotSupportedException($"The property {metadata.Name} is a collection. It can be indexed as a dictionary but not as an ordered index");
                        }

                        var keyValues = values.Cast<object>().Select(v => new KeyValue(v, metadata));
                        result.CollectionValues[collectionPos++] = new KeyValues(metadata.Name, keyValues);
                    }
                    else
                    {
                        throw new NotSupportedException($"The property {metadata.Name} is declared as a collection in the schema but the value is not a collection: {value.GetType().FullName}");
                    }
                }
                
            }

            
            // process full text
            var lines = new List<string>();

            foreach (var fulltext in typeDescription.FullText)
            {
                lines.AddRange(ExpressionTreeHelper.GetStringValues(instance, fulltext));
            }
                

            result.FullText = lines.ToArray();


            result.ObjectData =
                SerializationHelper.ObjectToBytes(instance, SerializationMode.Json, typeDescription.UseCompression);

            result.CollectionName = collectionName ?? typeDescription.CollectionName;

            result.UseCompression = typeDescription.UseCompression;


            return result;
        }


        

        static KeyValue JTokenToKeyValue(JToken jToken, KeyInfo info)
        {
            // as we ignore default values on json serialization 
            // the value can be absent because it is an int value 0
             
            if(jToken == null) return info.IndexType == IndexType.Primary?  new KeyValue(0, info): new KeyValue(null, info);

            var valueToken = jToken.HasValues ? jToken.First : jToken;

            if (valueToken?.Type == JTokenType.Integer)
            {
                return new KeyValue((long)valueToken, info);
            }

            if (valueToken?.Type == JTokenType.Float)
            {
                return new KeyValue((double)valueToken, info);
            }


            if (valueToken?.Type == JTokenType.Boolean)
            {
                return new KeyValue((bool)valueToken, info);
            }

            if (valueToken?.Type == JTokenType.Date)
            {
                return new KeyValue((DateTime)valueToken, info);
            }

            return new KeyValue((string)valueToken, info);

        }

        public string Json
        {
            get
            {
                
                var stream = new MemoryStream(ObjectData);
                if (UseCompression)
                {
                    var zInStream = new GZipInputStream(stream);
                    return new StreamReader(zInStream).ReadToEnd();

                }


                return new StreamReader(stream).ReadToEnd();
            }
        }

        /// <summary>
        /// Transform selected properties to a JObject. Alias names cam be specified
        /// </summary>
        /// <param name="valuesOrder"></param>
        /// <param name="valueNames">names in the returned json</param>
        /// <returns></returns>
        private JObject ToJObject(int[] valuesOrder, [CanBeNull] string[] valueNames)
        {
            
            // if only one value is specified, serialize it as primitive type
            //if (valuesOrder.Length == 1)
            //{
            //    var kv = Values[0];
            //    var jp = kv.ToJson();

            //    return jp.Value;
            //}
            
            
            var result = new JObject();

            int index = 0;
            foreach (var i in valuesOrder)
            {
                if (i < Values.Length) // scalar value
                {
                    var kv = Values[i];
                    var jp = kv.ToJson();

                    if (valueNames != null)
                    {
                        jp = new JProperty(valueNames[index], jp.Value);
                    }
                    

                    result.Add(jp);
                }
                else // collections
                {
                    var collectionIndex = i - Values.Length;

                    var collection = CollectionValues[collectionIndex];

                    var array = new JArray();
                    foreach (var keyValue in collection.Values)
                    {
                        array.Add(keyValue.ToJson().Value);
                    }

                    result.Add(collection.Name, array);
                }

                index++;
            }

            return result;
        }


        /// <summary>
        /// Return object data. It may be complete data or a projection if <see cref="valuesOrder"/> contains any element
        /// </summary>
        /// <param name="valuesOrder">indexes of values to be serialized</param>
        /// <param name="valueNames">optional names to replace the original property name</param>
        /// <returns></returns>
        public byte[] GetData(int[] valuesOrder, string[] valueNames = null)
        {
            if (valuesOrder.Length > 0)
            {
                return SerializationHelper.ObjectToBytes(ToJObject(valuesOrder, valueNames), SerializationMode.Json, UseCompression);
            }

            return ObjectData;
        }


        public byte[] GetServerSideData()
        {
            var result = new JObject();


            foreach (var v in Values)
            {
                var jp = v.ToJson();
                result.Add(jp);
            }

            foreach (var collection in CollectionValues)
            {
                var array = new JArray();
                foreach (var keyValue in collection.Values)
                {
                    array.Add(keyValue.ToJson().Value);
                }

                result.Add(collection.Name, array);
            }


            return SerializationHelper.ObjectToBytes(result, SerializationMode.Json, UseCompression);

        }


        /// <summary>
        /// Pack data that is not represented as a dotnet object
        /// </summary>
        /// <param name="propertyValues"></param>
        /// <param name="description"></param>
        /// <returns></returns>
        public static PackedObject PackDictionary(IDictionary<string, object> propertyValues,
            CollectionSchema description)
        {
            
            var json = JsonConvert.SerializeObject(propertyValues);

            return PackJson(json, description);

        }

        public static PackedObject PackJson(string json, CollectionSchema collectionSchema)
        {
            var jObject = JObject.Parse(json);
            return PackJson(jObject, collectionSchema);
        }
        
        public static PackedObject PackJson(JObject jObject, CollectionSchema collectionSchema, string collectionName = null)
        {
            
            if (jObject.Type == JTokenType.Array) throw new NotSupportedException("Pack called on a json array");

            var result = new PackedObject
            {
                // scalar values that are visible server-side
                Values = new KeyValue[collectionSchema.ServerSide.Count(k=>!k.IsCollection)],
                // vector values that are visible server-side
                CollectionValues = new KeyValues[collectionSchema.ServerSide.Count(k=>k.IsCollection)]
            };

            // process server-side values
            var pos = 0;
            var collectionPos = 0;
            foreach (var metadata in collectionSchema.ServerSide)
            {
                var jKey = jObject.Property(metadata.JsonName);

                
                if (!metadata.IsCollection)
                {
                    result.Values[pos++] = JTokenToKeyValue(jKey, metadata);
                }
                else 
                {
                    if (jKey?.Value.Type == JTokenType.Array)
                    {
                        if (metadata.IndexType == IndexType.Ordered)
                        {
                            throw new NotSupportedException($"The property {metadata.Name} is a collection. It can be indexed as a dictionary but not as an ordered index");
                        }

                        var keyValues = jKey.Value.Children().Select(j => JTokenToKeyValue(j, metadata));

                        result.CollectionValues[collectionPos++] = new KeyValues(metadata.Name, keyValues);
                    }
                    else if(jKey?.Value == null) // create an empty collection if no data
                    {
                        result.CollectionValues[collectionPos++] = new KeyValues(metadata.Name, Enumerable.Empty<KeyValue>());
                    }
                    else
                    {
                        throw new NotSupportedException($"The property {metadata.Name} is declared as a collection in the schema but the value is not a collection: {jKey?.Name} ");
                    }
                }
                
            }
            


            // process full text
            var lines = new List<string>();

            foreach (var fulltext in collectionSchema.FullText)
            {
                var jKey = jObject.Property(fulltext);

                if (jKey == null)
                    continue;

                if (jKey.Value.Type == JTokenType.Array)
                    foreach (var jToken in jKey.Value.Children())
                        if (jToken.Type == JTokenType.String)
                        {
                            lines.Add((string) jToken);
                        }
                        else
                        {
                            var child = (JObject) jToken;

                            foreach (var jToken1 in child.Children())
                            {
                                var field = (JProperty) jToken1;
                                if (field.Value.Type == JTokenType.String && !field.Name.StartsWith("$"))
                                    lines.Add((string) field);
                            }
                        }
                else
                    lines.Add((string) jKey);
            }

            result.FullText = lines.ToArray();

           
            result.ObjectData = SerializationHelper.ObjectToBytes(jObject, SerializationMode.Json, collectionSchema.UseCompression);

            result.CollectionName = collectionName ?? collectionSchema.CollectionName;

            result.UseCompression = collectionSchema.UseCompression;

            
            return result;
        }

        

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(PrimaryKey);
            sb.AppendLine(" {");

          
            if (Values != null && Values.Length > 0)
            {
                sb.Append("  values:");
                foreach (var key in Values)
                {
                    sb.Append(key);
                    sb.Append(" ");
                }
                sb.AppendLine();
            }

            if (FullText != null && FullText.Length > 0)
            {
                sb.AppendLine("  full text:");
                foreach (var key in FullText)
                {
                    sb.AppendLine($"    {key}");
                    
                }
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
        /// <returns> </returns>
        public static T Unpack<T>(PackedObject packedObject)
        {
            return SerializationHelper.ObjectFromBytes<T>(packedObject.ObjectData, SerializationMode.Json,
                packedObject.UseCompression);
        }

        private bool Equals(PackedObject other)
        {
            return PrimaryKey.Equals(other.PrimaryKey);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != GetType())
                return false;
            return Equals((PackedObject) obj);
        }

        public override int GetHashCode()
        {
            return PrimaryKey.GetHashCode();
        }

        public static bool operator ==(PackedObject left, PackedObject right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(PackedObject left, PackedObject right)
        {
            return !Equals(left, right);
        }

        public KeyValue this[int order] => Values[order];

        public KeyValues Collection(int order)
        {
            // in a collection schema the vector values are positioned after scalar values
            var pos = order - Values.Length;

            return CollectionValues[pos];
        }
    }
}