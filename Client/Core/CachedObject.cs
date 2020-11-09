using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Client.Core.TypeConverters;
using Client.Interface;
using Client.Messages;
using ICSharpCode.SharpZipLib.GZip;
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
        public string FullTypeName { get; private set; }


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


        public string GlobalKey => FullTypeName + PrimaryKey;

        
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
        public static CachedObject Pack<TObject>(TObject instance, ClientSideTypeDescription typeDescription = null)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            var netType = instance.GetType();

            typeDescription ??= ClientSideTypeDescription.RegisterType<TObject>();

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
                SerializationHelper.ObjectToBytes(instance, SerializationMode.Json, typeDescription.AsTypeDescription);
            result.FullTypeName = typeDescription.FullTypeName;

            result.UseCompression = typeDescription.UseCompression;

            return result;
        }


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

        public static double JTokenToDouble(JToken jToken)
        {
            // null converted to long is 0
            if (jToken == null) return 0;


            var valueToken = jToken.HasValues ? jToken.First : jToken;

            if (valueToken?.Type == JTokenType.Integer)
            {
                var doubleValue= (double?) valueToken;

                if (doubleValue.HasValue)
                {
                    return doubleValue.Value;

                }
            }
            else if(valueToken?.Type == JTokenType.Float)
            {
                var doubleValue= (double?) valueToken;

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

        public static CachedObject PackJson(string json, TypeDescription typeDescription)
        {
            var jObject = JObject.Parse(json);

            if (jObject.Type == JTokenType.Array) throw new NotSupportedException("Pack called on a json array");

            CachedObject result;

            var jPrimary = jObject.Property(typeDescription.PrimaryKeyField.Name);
            if (typeDescription.PrimaryKeyField.KeyDataType == KeyDataType.IntKey)
            {
                var primaryKey = new KeyValue(JTokenToLong(jPrimary), typeDescription.PrimaryKeyField);
                result = new CachedObject(primaryKey);
            }
            else
            {
                var primaryKey = new KeyValue(JTokenToString(jPrimary), typeDescription.PrimaryKeyField);
                result = new CachedObject(primaryKey);
            }


            result.UniqueKeys = new KeyValue[typeDescription.UniqueKeyFields.Count];

            var pos = 0;
            foreach (var uniqueField in typeDescription.UniqueKeyFields)
            {
                var jKey = jObject.Property(uniqueField.Name);
                var key = uniqueField.KeyDataType == KeyDataType.IntKey
                    ? new KeyValue(JTokenToLong(jKey), uniqueField)
                    : new KeyValue(JTokenToString(jKey), uniqueField);

                result.UniqueKeys[pos++] = key;
            }


            result.IndexKeys = new KeyValue[typeDescription.IndexFields.Count];
            pos = 0;
            foreach (var indexField in typeDescription.IndexFields)
            {
                var jKey = jObject.Property(indexField.Name);
                var key = indexField.KeyDataType == KeyDataType.IntKey
                    ? new KeyValue(JTokenToLong(jKey), indexField)
                    : new KeyValue(JTokenToString(jKey), indexField);

                result.IndexKeys[pos++] = key;
            }

            // process indexed collections
            var listValues = new List<KeyValue>();
            foreach (var indexField in typeDescription.ListFields)
            {
                var jArray = jObject.Property(indexField.Name);

                if (jArray != null)
                    foreach (var jKey in jArray.Value.Children())
                    {
                        var key = indexField.KeyDataType == KeyDataType.IntKey
                            ? new KeyValue(JTokenToLong(jKey), indexField)
                            : new KeyValue(JTokenToString(jKey), indexField);

                        listValues.Add(key);
                    }
            }

            result.ListIndexKeys = listValues.ToArray();


            // process server side values

            var serverValues = new List<ServerSideValue>();
            pos = 0;
            foreach (var value in typeDescription.ServerSideValues)
            {
                var jKey = jObject.Property(value.Name);

                var val = JTokenToDouble(jKey);


                serverValues.Add( new ServerSideValue{Name = value.Name, Value = val});
            }

            result.Values = serverValues.ToArray();


            // process full text
            var lines = new List<string>();

            foreach (var fulltext in typeDescription.FullText)
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

            using (var output = new MemoryStream())
            {
                if (typeDescription.UseCompression)
                {
                    var outZStream = new GZipOutputStream(output) {IsStreamOwner = false};
                    var encoded = Encoding.UTF8.GetBytes(json);
                    outZStream.Write(encoded, 0, encoded.Length);

                    outZStream.Flush();
                }
                else
                {
                    var encoded = Encoding.UTF8.GetBytes(json);
                    output.Write(encoded, 0, encoded.Length);
                }


                result.ObjectData = output.ToArray();
            }

            result.FullTypeName = typeDescription.FullTypeName;

            result.UseCompression = typeDescription.UseCompression;

            return result;
        }

        public static CachedObject Pack(object obj, TypeDescription description)
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
    }
}