using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Client.Messages;
using ICSharpCode.SharpZipLib.GZip;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProtoBuf;

namespace Client.Core
{
    /// <summary>
    ///     Any object is converted to this form while stored in the cache and transferred through the
    ///     network. Only key fields are available, the rest of the object is serialized as json
    /// </summary>
    [ProtoContract]
    public class CachedObject
    {
        /// <summary>
        ///     This property is not persistent. It is used when ordering items from multiple nodes
        /// </summary>
        public double Rank { get; set; }


        /// <summary>
        ///     Full name of the defining type
        /// </summary>
        [field: ProtoMember(1)]
        public string CollectionName { get; private set; }


        /// <summary>
        ///     The one and only primary key
        /// </summary>
        [field: ProtoMember(2)]
        public KeyValue PrimaryKey { get; }

        [field: ProtoMember(3)] public KeyValue[] UniqueKeys { get; private set; }

        [field: ProtoMember(4)] public KeyValue[] IndexKeys { get; private set; }


        /// <summary>
        ///     The original object serialized to byte[]
        /// </summary>
        [field: ProtoMember(5)]
        public byte[] ObjectData { get; private set; }

        [field: ProtoMember(7)] public bool UseCompression { get; private set; }

        /// <summary>
        ///     Keys that can be used for "Contains" queries
        /// </summary>
        [field: ProtoMember(8)]
        public KeyValue[] ListIndexKeys { get; private set; }

        [field: ProtoMember(9)] public string[] FullText { get; private set; }

        /// <summary>
        ///     Store tokenized full text to avoid tokenization time while loading database from storage
        /// </summary>
        [field: ProtoMember(10)]
        public IList<TokenizedLine> TokenizedFullText { get; set; }

        [field: ProtoMember(11)] public KeyValue[] Values { get; private set; }

        /// <summary>
        ///     Default constructor for serialization only
        /// </summary>
        // ReSharper disable once UnusedMember.Local
        private CachedObject()
        {
        }

        private CachedObject(KeyValue primaryKey)
        {
            PrimaryKey = primaryKey;
        }


        public string GlobalKey => CollectionName + PrimaryKey;

        
        public bool MatchOneOf(ISet<KeyValue> values)
        {
            var indexType = values.First().KeyType;
            var indexName = values.First().KeyName;

            if (indexType == IndexType.Primary)
                return values.Contains(PrimaryKey);


            if (indexType == IndexType.Unique && UniqueKeys != null)
                return UniqueKeys.Where(i => i.KeyName == indexName).Any(values.Contains);

            if ((indexType == IndexType.Dictionary || indexType == IndexType.Ordered) && IndexKeys != null)
                return IndexKeys.Where(i => i.KeyName == indexName).Any(values.Contains);

            // TODO manage non indexed server values

            return false;
        }


        public static CachedObject Pack<TObject>(TObject instance, CollectionSchema typeDescription)
        {

            if (instance == null) throw new ArgumentNullException(nameof(instance));

            var netType = instance.GetType();


            if (typeDescription == null)
                throw new NotSupportedException(
                    $"Can not pack an object of type {netType} because the type was not registered");

            var getter = ExpressionTreeHelper.Getter<TObject>(typeDescription.PrimaryKeyField.Name);
            var value = getter(instance);

            // auto generate an empty guid primary key
            if (Guid.Empty.Equals(value))
            {
                value = Guid.NewGuid();
            }

            var result = new CachedObject(new KeyValue(value, typeDescription.PrimaryKeyField))
            {
                UniqueKeys = new KeyValue[typeDescription.UniqueKeyFields.Count],
            };


            // process unique keys
            var pos = 0;
            foreach (var uniqueField in typeDescription.UniqueKeyFields)
            {
                getter = ExpressionTreeHelper.Getter<TObject>(uniqueField.Name);
                result.UniqueKeys[pos++] = new KeyValue(getter(instance), uniqueField);
            }

            
            // process index keys
            var listKeys = new List<KeyValue>();
            var indexKeys = new List<KeyValue>();
            foreach (var indexField in typeDescription.IndexFields)
            {
                getter = ExpressionTreeHelper.Getter<TObject>(indexField.Name);
                value = getter(instance);

                // check if the value is a collection. Strings are IEnumerable but they mut not be treated as collections
                if(value is IEnumerable values && !(value is string))
                {
                    if (indexField.IndexType == IndexType.Ordered)
                    {
                        throw new NotSupportedException($"The property {indexField.Name} is a collection. It can be indexed as a dictionary but not as an ordered index");
                    }
                    var keyValues = values.Cast<object>().Select(v => new KeyValue(v, indexField));
                    listKeys.AddRange(keyValues);
                }
                else
                {
                    indexKeys.Add(new KeyValue(value, indexField));
                }
            }
            result.ListIndexKeys = listKeys.ToArray();
            result.IndexKeys = indexKeys.ToArray();

            
            // process non indexed server-side values
            var serverValues = new List<KeyValue>();
            foreach (var serverValue in typeDescription.ServerSideValues)
            {
                getter = ExpressionTreeHelper.Getter<TObject>(serverValue.Name);
                serverValues.Add(new KeyValue(getter(instance), serverValue));
            }

            result.Values = serverValues.ToArray();    


            // process full text
            var lines = new List<string>();

            foreach (var fulltext in typeDescription.FullText)
            {
                lines.AddRange(ExpressionTreeHelper.GetStringValues(instance, fulltext));
            }
                

            result.FullText = lines.ToArray();


            result.ObjectData =
                SerializationHelper.ObjectToBytes(instance, SerializationMode.Json, typeDescription);
            result.CollectionName = typeDescription.CollectionName;

            result.UseCompression = typeDescription.UseCompression;

            
            return result;
        }



        static KeyValue JTokenToKeyValue(JToken jToken, KeyInfo info)
        {
            if(jToken == null) return new KeyValue(null, info);

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
        /// Pack data that is not represented as a dotnet object
        /// </summary>
        /// <param name="propertyValues"></param>
        /// <param name="description"></param>
        /// <returns></returns>
        public static CachedObject PackDictionary(IDictionary<string, object> propertyValues,
            CollectionSchema description)
        {
            
            var json = JsonConvert.SerializeObject(propertyValues);

            return PackJson(json, description);

        }

        public static CachedObject PackJson(string json, CollectionSchema collectionSchema)
        {
            var jObject = JObject.Parse(json);
            return PackJson(jObject, collectionSchema);
        }
        
        public static CachedObject PackJson(JObject jObject, CollectionSchema collectionSchema)
        {
            

            if (jObject.Type == JTokenType.Array) throw new NotSupportedException("Pack called on a json array");

            CachedObject result;

            var jPrimary = jObject.Property(collectionSchema.PrimaryKeyField.JsonName);

            if (jPrimary == null )
            {
                // TODO specify automatic primary key
                //result = new CachedObject(new KeyValue(Guid.NewGuid().ToString(), collectionSchema.PrimaryKeyField));
                result = new CachedObject(new KeyValue(0, collectionSchema.PrimaryKeyField));
            }
            else
            {
                var primaryKey = JTokenToKeyValue(jPrimary, collectionSchema.PrimaryKeyField);

                result = new CachedObject(primaryKey);
            }

            

            var uniqueKeys = new List<KeyValue>(collectionSchema.UniqueKeyFields.Count);
            
            foreach (var uniqueField in collectionSchema.UniqueKeyFields)
            {
                var jKey = jObject.Property(uniqueField.JsonName);

                uniqueKeys.Add(JTokenToKeyValue(jKey, uniqueField));
            }

            result.UniqueKeys = uniqueKeys.ToArray();


            var listValues = new List<KeyValue>();
            var indexKeys = new List<KeyValue>(collectionSchema.IndexFields.Count);

            foreach (var indexField in collectionSchema.IndexFields)
            {
                var jKey = jObject.Property(indexField.JsonName);

                if (jKey?.Value.Type == JTokenType.Array)
                {
                    foreach (var jValue in jKey.Value.Children())
                    {
                        listValues.Add(JTokenToKeyValue(jValue, indexField));
                    }
                }
                else
                {
                    indexKeys.Add(JTokenToKeyValue(jKey, indexField));
                }
               
                
            }

            result.IndexKeys = indexKeys.ToArray();
            result.ListIndexKeys = listValues.ToArray();


            // process server side values

            var serverValues = new List<KeyValue>();
            foreach (var value in collectionSchema.ServerSideValues)
            {
                var jKey = jObject.Property(value.JsonName);
                serverValues.Add( JTokenToKeyValue(jKey, value));
            }

            result.Values = serverValues.ToArray();


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

           
            result.ObjectData = SerializationHelper.ObjectToBytes(jObject, SerializationMode.Json, collectionSchema);

            result.CollectionName = collectionSchema.CollectionName;

            result.UseCompression = collectionSchema.UseCompression;

            return result;
        }

        

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(PrimaryKey);
            sb.AppendLine(" {");

            if (UniqueKeys != null && UniqueKeys.Length > 0)
            {
                sb.Append("  unique:");
                foreach (var key in UniqueKeys)
                {
                    sb.Append(key);
                    sb.Append(" ");
                }
                sb.AppendLine();
            }
                

            if (IndexKeys != null && IndexKeys.Length > 0)
            {
                sb.Append("  scalar:");
                foreach (var key in IndexKeys)
                {
                    sb.Append(key);
                    sb.Append(" ");
                }
                sb.AppendLine();
            }
                

            if (ListIndexKeys != null && ListIndexKeys.Length > 0)
            {

                sb.Append("  collections:");
                foreach (var key in ListIndexKeys)
                {
                    sb.Append(key);
                    sb.Append(" ");
                }
                sb.AppendLine();
            }

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
        /// <param name="cachedObject"> </param>
        /// <returns> </returns>
        public static T Unpack<T>(CachedObject cachedObject)
        {
            return SerializationHelper.ObjectFromBytes<T>(cachedObject.ObjectData, SerializationMode.Json,
                cachedObject.UseCompression);
        }

        private bool Equals(CachedObject other)
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
            return Equals((CachedObject) obj);
        }

        public override int GetHashCode()
        {
            return PrimaryKey.GetHashCode();
        }

        public static bool operator ==(CachedObject left, CachedObject right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(CachedObject left, CachedObject right)
        {
            return !Equals(left, right);
        }

        public KeyValue this[string name]
        {
            get
            {

                if (PrimaryKey.KeyName == name)
                {
                    return PrimaryKey;
                }

                return IndexKeys.FirstOrDefault(k => k.KeyName == name) ??
                       Values.FirstOrDefault(k => k.KeyName == name) ??
                       UniqueKeys.FirstOrDefault(k => k.KeyName == name) ?? 
                       throw new KeyNotFoundException($"Can not find the property {name}");
            }
        }
    }
}