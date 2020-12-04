using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Client.Core.TypeConverters;
using Client.Interface;
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
        public KeyValue PrimaryKey { get; set; }

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

        [field: ProtoMember(11)] public ServerSideValue[] Values { get; private set; }

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

            if (indexType == KeyType.Primary)
                return values.Contains(PrimaryKey);


            if (indexType == KeyType.Unique && UniqueKeys != null)
                return UniqueKeys.Where(i => i.KeyName == indexName).Any(values.Contains);

            if (indexType == KeyType.ScalarIndex && IndexKeys != null)
                return IndexKeys.Where(i => i.KeyName == indexName).Any(values.Contains);

            if (indexType == KeyType.ListIndex && ListIndexKeys != null)
                return ListIndexKeys.Where(i => i.KeyName == indexName).Any(values.Contains);

            return false;
        }

        /// <summary>
        ///     Factory method : converts an object to <see cref="CachedObject" />
        ///     The type of the object needs to be previously registered
        /// </summary>
        /// <returns> </returns>
        public static CachedObject Pack<TObject>(TObject instance, ClientSideTypeDescription typeDescription = null, string collectionName = null)
        {

            if (instance == null) throw new ArgumentNullException(nameof(instance));

            var netType = instance.GetType();

            typeDescription ??= ClientSideTypeDescription.RegisterType<TObject>();

            var collection = collectionName ?? typeDescription.FullTypeName;

            if (typeDescription == null)
                throw new NotSupportedException(
                    $"Can not pack an object of type {netType} because the type was not registered");

            var result = new CachedObject(typeDescription.PrimaryKeyField.GetValue(instance))
            {
                UniqueKeys = new KeyValue[typeDescription.UniqueKeysCount]
            };

            var pos = 0;
            foreach (var uniqueField in typeDescription.UniqueKeyFields)
                result.UniqueKeys[pos++] = uniqueField.GetValue(instance);

            result.IndexKeys = new KeyValue[typeDescription.IndexCount];
            pos = 0;
            foreach (var indexField in typeDescription.IndexFields)
                result.IndexKeys[pos++] = indexField.GetValue(instance);

            result.Values = new ServerSideValue[typeDescription.ServerValuesCount];
            pos = 0;
            foreach (var serverValue in typeDescription.ServerSideValues)
                result.Values[pos++] = serverValue.GetServerValue(instance);

            // process indexed collections

            var listKeys = new List<KeyValue>();

            foreach (var indexField in typeDescription.ListFields)
            {
                var values = indexField.GetCollectionValues(instance).ToArray();
                listKeys.AddRange(values);
            }

            result.ListIndexKeys = listKeys.ToArray();


            // process full text
            var lines = new List<string>();

            foreach (var fulltext in typeDescription.FullTextIndexed)
                lines.AddRange(fulltext.GetStringValues(instance));

            result.FullText = lines.ToArray();


            result.ObjectData =
                SerializationHelper.ObjectToBytes(instance, SerializationMode.Json, typeDescription.AsCollectionSchema);
            result.CollectionName = typeDescription.FullTypeName;

            result.UseCompression = typeDescription.UseCompression;

            result.CollectionName = collection;

            return result;
        }

        //public static CachedObject FastPack<TObject>(TObject instance, CollectionSchema typeDescription, string collectionName)
        //{

        //    if (instance == null) throw new ArgumentNullException(nameof(instance));

        //    var netType = instance.GetType();


        //    if (typeDescription == null)
        //        throw new NotSupportedException(
        //            $"Can not pack an object of type {netType} because the type was not registered");

        //    var getter = ExpressionTreeHelper.Getter<TObject>(typeDescription.PrimaryKeyField.Name);

        //    var result = new CachedObject(new KeyValue(getter(instance), typeDescription.PrimaryKeyField))
        //    {
        //        UniqueKeys = new KeyValue[typeDescription.UniqueKeyFields.Count]
        //    };

        //    var pos = 0;
        //    foreach (var uniqueField in typeDescription.UniqueKeyFields)
        //    {
        //        getter = ExpressionTreeHelper.Getter<TObject>(uniqueField.Name);
        //        result.UniqueKeys[pos++] = new KeyValue(getter(instance), uniqueField);
        //    }
                

        //    result.IndexKeys = new KeyValue[typeDescription.IndexFields.Count];
        //    pos = 0;
        //    foreach (var indexField in typeDescription.IndexFields)
        //    {
        //        getter = ExpressionTreeHelper.Getter<TObject>(indexField.Name);
        //        result.IndexKeys[pos++] = new KeyValue(getter(instance), indexField);
        //    }
                

        //    result.Values = new ServerSideValue[typeDescription.ServerValuesCount];
        //    pos = 0;
        //    foreach (var serverValue in typeDescription.ServerSideValues)
        //        result.Values[pos++] = serverValue.GetServerValue(instance);

        //    // process indexed collections

        //    var listKeys = new List<KeyValue>();

        //    foreach (var indexField in typeDescription.ListFields)
        //    {
        //        var values = indexField.GetCollectionValues(instance).ToArray();
        //        listKeys.AddRange(values);
        //    }

        //    result.ListIndexKeys = listKeys.ToArray();


        //    // process full text
        //    var lines = new List<string>();

        //    foreach (var fulltext in typeDescription.FullTextIndexed)
        //        lines.AddRange(fulltext.GetStringValues(instance));

        //    result.FullText = lines.ToArray();


        //    result.ObjectData =
        //        SerializationHelper.ObjectToBytes(instance, SerializationMode.Json, typeDescription.AsCollectionSchema);
        //    result.CollectionName = typeDescription.FullTypeName;

        //    result.UseCompression = typeDescription.UseCompression;

        //    result.CollectionName = collection;

        //    return result;
        //}

        public static bool CanBeConvertedToLong(JToken jToken)
        {
            // null converted to long is 0
            if (jToken == null) return true;


            var valueToken = jToken.HasValues ? jToken.First : jToken;

            if (valueToken == null)
            {
                return false;
            }

            var type = valueToken.Type;

            if (type == JTokenType.Boolean || type == JTokenType.Date || type == JTokenType.Float ||
                type == JTokenType.Integer)
            {
                return true;
            }

            return false;
        }

        // TODO move this logic to KeyValue
        public static long JTokenToLong(JToken jToken)
        {
            // null converted to long is 0
            if (jToken == null) return 0;


            var valueToken = jToken.HasValues ? jToken.First : jToken;

            if (valueToken?.Type == JTokenType.Date)
            {
                var dateTime = (DateTime?) valueToken;

                if (dateTime.HasValue)
                {
                    var value = dateTime.Value;

                    return new DateTimeConverter().GetAsLong(value);
                }
            }

            if (valueToken?.Type == JTokenType.Float) return new DoubleConverter().GetAsLong((double) valueToken);

            return (long) valueToken;
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
        public static decimal JTokenToDecimal(JToken jToken)
        {
            // null converted to long is 0
            if (jToken == null) return 0;


            var valueToken = jToken.HasValues ? jToken.First : jToken;

            if (valueToken?.Type == JTokenType.Integer)
            {
                var doubleValue= (decimal?) valueToken;

                if (doubleValue.HasValue)
                {
                    return doubleValue.Value;
                }
            }
            else if(valueToken?.Type == JTokenType.Float)
            {
                var doubleValue= (decimal?) valueToken;

                if (doubleValue.HasValue)
                {
                    return doubleValue.Value;
                }
            }

            throw new FormatException("can not convert value to double");
        }

        public static string JTokenToString(JToken jToken)
        {
            if (jToken == null) return null;

            return (string) jToken;
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

            var jPrimary = jObject.Property(collectionSchema.PrimaryKeyField.Name);

            if (jPrimary == null && collectionSchema.PrimaryKeyField.KeyDataType == KeyDataType.Generate)
            {
                result = new CachedObject(new KeyValue(Guid.NewGuid().ToString(), collectionSchema.PrimaryKeyField));
            }
            else
            {
                if (collectionSchema.PrimaryKeyField.KeyDataType == KeyDataType.IntKey ||
                    (collectionSchema.PrimaryKeyField.KeyDataType == KeyDataType.Default &&
                     CanBeConvertedToLong(jPrimary)))
                {
                    var primaryKey = new KeyValue(JTokenToLong(jPrimary), collectionSchema.PrimaryKeyField);
                    result = new CachedObject(primaryKey);
                }
                else
                {
                    var primaryKey = new KeyValue(JTokenToString(jPrimary), collectionSchema.PrimaryKeyField);
                    result = new CachedObject(primaryKey);
                }    
            }

            
            


            var uniqueKeys = new List<KeyValue>(collectionSchema.UniqueKeyFields.Count);
            
            foreach (var uniqueField in collectionSchema.UniqueKeyFields)
            {
                var jKey = jObject.Property(uniqueField.Name);

                
                var key = uniqueField.KeyDataType == KeyDataType.IntKey || (uniqueField.KeyDataType == KeyDataType.Default && CanBeConvertedToLong(jKey))
                    ? new KeyValue(JTokenToLong(jKey), uniqueField)
                    : new KeyValue(JTokenToString(jKey), uniqueField);

                uniqueKeys.Add(key);
            
            }

            result.UniqueKeys = uniqueKeys.ToArray();


            
            var indexKeys = new List<KeyValue>(collectionSchema.IndexFields.Count);

            foreach (var indexField in collectionSchema.IndexFields)
            {
                var jKey = jObject.Property(indexField.Name);
               
                    var key = indexField.KeyDataType == KeyDataType.IntKey || (indexField.KeyDataType == KeyDataType.Default && CanBeConvertedToLong(jKey))
                        ? new KeyValue(JTokenToLong(jKey), indexField)
                        : new KeyValue(JTokenToString(jKey), indexField);

                    indexKeys.Add(key);
            }

            result.IndexKeys = indexKeys.ToArray();

            // process indexed collections
            var listValues = new List<KeyValue>();
            foreach (var indexField in collectionSchema.ListFields)
            {
                var jArray = jObject.Property(indexField.Name);

                if (jArray != null)
                    foreach (var jKey in jArray.Value.Children())
                    {
                        var key = indexField.KeyDataType == KeyDataType.IntKey || (indexField.KeyDataType == KeyDataType.Default && CanBeConvertedToLong(jKey))
                            ? new KeyValue(JTokenToLong(jKey), indexField)
                            : new KeyValue(JTokenToString(jKey), indexField);

                        listValues.Add(key);
                    }
            }

            result.ListIndexKeys = listValues.ToArray();


            // process server side values

            var serverValues = new List<ServerSideValue>();
            foreach (var value in collectionSchema.ServerSideValues)
            {
                var jKey = jObject.Property(value.Name);

                var val = JTokenToDecimal(jKey);


                serverValues.Add( new ServerSideValue{Name = value.Name, Value = val});
            }

            result.Values = serverValues.ToArray();


            // process full text
            var lines = new List<string>();

            foreach (var fulltext in collectionSchema.FullText)
            {
                var jKey = jObject.Property(fulltext.Name);

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

            //using (var output = new MemoryStream())
            //{
            //    if (collectionSchema.UseCompression)
            //    {
            //        var outZStream = new GZipOutputStream(output) {IsStreamOwner = false};
            //        var encoded = Encoding.UTF8.GetBytes(jObject.ToString());
            //        outZStream.Write(encoded, 0, encoded.Length);

            //        outZStream.Flush();
            //    }
            //    else
            //    {
            //        var encoded = Encoding.UTF8.GetBytes(jObject.ToString());
            //        output.Write(encoded, 0, encoded.Length);
            //    }


            //    result.ObjectData = output.ToArray();
            //}

            result.ObjectData = SerializationHelper.ObjectToBytes(jObject, SerializationMode.Json, collectionSchema);

            result.CollectionName = collectionSchema.CollectionName;

            result.UseCompression = collectionSchema.UseCompression;

            return result;
        }

        public static CachedObject Pack(object obj, CollectionSchema description)
        {
            var json = SerializationHelper.ObjectToJson(obj);
            return PackJson(json, description);
        }


        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(PrimaryKey);
            sb.Append(" {");

            if (UniqueKeys != null)
                foreach (var key in UniqueKeys)
                {
                    sb.Append(key);
                    sb.Append(" ");
                }

            if (IndexKeys != null)
                foreach (var key in IndexKeys)
                {
                    sb.Append(key);
                    sb.Append(" ");
                }

            if (ListIndexKeys != null)
                foreach (var key in ListIndexKeys)
                {
                    sb.Append(key);
                    sb.Append(" ");
                }

            sb.Append(" } ");

            if (ObjectData != null)
                sb.Append(ObjectData.Length).Append(" bytes");

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
                       UniqueKeys.FirstOrDefault(k => k.KeyName == name) ?? throw new KeyNotFoundException($"Can not find the property {name}");
            }
        }
    }
}