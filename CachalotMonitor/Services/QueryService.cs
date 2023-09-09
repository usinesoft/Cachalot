using System.Collections.Concurrent;
using System.Text;
using Cachalot.Linq;
using CachalotMonitor.Model;
using Client.Core;
using Client.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CachalotMonitor.Services;

internal class QueryService : IQueryService
{
    private readonly IClusterService _clusterService;

    public QueryService(IClusterService clusterService)
    {
        _clusterService = clusterService ?? throw new ArgumentNullException(nameof(clusterService));

        ActivityTable = clusterService.Connector?.DataSource<LogEntry>("@ACTIVITY");
    }

    private DataSource<LogEntry>? ActivityTable { get; }


    public string QueryAsJson(string? sql, string? fullTextQuery = null, Guid queryId = default)
    {
        var result = _clusterService.Connector?.SqlQueryAsJson(sql, fullTextQuery, queryId).ToList();

        if (result != null)
        {
            var ja = new JArray(result);
            return ja.ToString(Formatting.None, new SmartDateTimeConverter());
        }

        return "[]";
    }

    public async Task QueryAsStream(Stream targetStream, string? sql, string? fullTextQuery = null)
    {
        await using var writer = new StreamWriter(targetStream, Encoding.UTF8);

        var result = _clusterService.Connector?.SqlQueryAsJson(sql, fullTextQuery);

        if (result == null)
            return;

        var count = 0;

        await writer.WriteAsync('[');

        foreach (var item in result)
        {
            if (count != 0) // write the comma after the previous object
                await writer.WriteAsync(',');

            await writer.WriteAsync(item.ToString(Formatting.None));

            count++;

            if (count % 1000 == 0) // flush every 1000 items
                await writer.FlushAsync();
        }

        await writer.WriteAsync(']');

        await writer.FlushAsync();
    }


    public async Task PutManyAsStream(Stream stream, string collectionName)
    {
        var streamedObjects = new BlockingCollection<JObject>();

        var consuming = Task.Run(() =>
        {
            _clusterService.Connector?.FeedWithJson(collectionName, ObjectConsumer(streamedObjects));
        });


        using var sr = new StreamReader(stream);
        using var reader = new JsonTextReader(sr);
        while (await reader.ReadAsync())
            if (reader.TokenType == JsonToken.StartObject)
            {
                // Load each object from the stream and do something with it
                var obj = await JObject.LoadAsync(reader);
                streamedObjects.Add(obj);
            }

        streamedObjects.CompleteAdding();

        await consuming;
    }

    public ExecutionPlan? GetExecutionPlan(Guid queryId)
    {
        if (ActivityTable != null)
        {
            var entries = ActivityTable.Where(x => x.Id == queryId).ToList();

            // aggregate matches from all servers
            var totalMatched = entries.Sum(x => x.ExecutionPlan.MatchedItems);

            // return one of the execution plans
            if (entries.Count > 0)
            {
                entries[0].ExecutionPlan.MatchedItems = totalMatched;
                return entries[0].ExecutionPlan;
            }
        }

        return null;
    }

    public QueryMetadata GetMetadata(string collection, string property)
    {
        try
        {
            var info = _clusterService.GetClusterInformation();

            var metadata = new QueryMetadata();

            var schema = info.Schema.FirstOrDefault(x =>
                x.CollectionName.Equals(collection, StringComparison.CurrentCultureIgnoreCase));

            if (schema != null)
            {
                var pr = schema.ServerSide.FirstOrDefault(x =>
                    x.Name.Equals(property, StringComparison.InvariantCultureIgnoreCase));

                if (pr != null)
                {
                    metadata.Found = true;
                    metadata.CollectionName = collection;
                    metadata.PropertyName = property;


                    // query distinct values of the property
                    var result = _clusterService.Connector
                        ?.SqlQueryAsJson(
                            $"select distinct {property} from {collection} take {QueryMetadata.MaxValues + 10}")
                        .ToList();

                    if (result?.Count == 0) // only if the table is empty
                        return metadata;

                    if (pr.IsCollection)
                        MetadataForCollectionProperty(property, result!, metadata);
                    else
                        MetadataForScalarProperty(property, result!, metadata);

                    // if more than max values do not load the network as the result can not be used for query definition
                    if (result!.Count > QueryMetadata.MaxValues) metadata.PossibleValues = Array.Empty<string>();

                    metadata.PossibleValuesCount = result.Count;

                    return metadata;
                }
            }
        }
        catch (Exception)
        {
            return new() { Found = false };
        }

        return new() { Found = false };
    }

    public string ClientQueryToSql(string collection, AndQuery query)
    {
        var builder = new StringBuilder();

        builder.Append("SELECT");
        builder.Append(" ");
        builder.Append("FROM");
        builder.Append(" ");
        builder.Append(collection);
        builder.Append(" ");


        if (query.SimpleQueries.Any(q => q.CheckIsValid()))
        {
            builder.Append("WHERE");
            builder.Append(Environment.NewLine);
        }

        for (var i = 0; i < query.SimpleQueries.Length; i++)
        {
            var q = query.SimpleQueries[i];
            SimpleQueryToSql(q, builder);
            if (i < query.SimpleQueries.Length - 1)
                if (query.SimpleQueries[i + 1].CheckIsValid())
                {
                    builder.Append(" ");
                    builder.Append("AND");
                    builder.Append(Environment.NewLine);
                    builder.Append(" ");
                }
        }

        if (query.OrderBy != null)
        {
            builder.Append(Environment.NewLine);
            builder.Append(" ");
            builder.Append("ORDER BY");
            builder.Append(" ");
            builder.Append(query.OrderBy);

            if (query.Descending)
            {
                builder.Append(" ");
                builder.Append("DESCENDING");
            }
        }

        builder.Append(Environment.NewLine);
        builder.Append($"TAKE {query.Take}");

        return builder.ToString();
    }


    private IEnumerable<JObject> ObjectConsumer(BlockingCollection<JObject> queue)
    {
        foreach (var item in queue.GetConsumingEnumerable()) yield return item;
    }

    private void SimpleQueryToSql(SimpleQuery query, StringBuilder builder)
    {
        string ValueToSql(string value, string? op = null)
        {
            if (query.DataType == PropertyType.String && !query.PropertyIsCollection)
            {
                if (op == "starts with") return $"'{value}%'";

                if (op == "ends with") return $"'%{value}'";

                if (op == "contains") return $"'%{value}%'";

                return $"'{value}'";
            }

            if (query.DataType == PropertyType.SomeFloat) return $"{value}";

            if (query.PropertyIsCollection)
            {
                if (query.DataType == PropertyType.String)
                    return $"'{value}'";
                return $"{value}";
            }

            return value;
        }

        if (query.CheckIsValid())
        {
            builder.Append(query.PropertyName);
            builder.Append(" ");

            // values may be either explicit list or a literal list separated by comma
            var values = new List<string>();

            if (query.Values.Length > 1)
            {
                values.AddRange(query.Values);
            }
            else if (query.Values.Length == 1)
            {
                var first = query.Values[0];
                if (first.Contains(','))
                    values.AddRange(first.Split(',',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                else
                    values.Add(first.Trim());
            }

            var needValue = true;

            switch (query.Operator)
            {
                case "is null":
                    builder.Append("= null");
                    needValue = false;
                    break;
                case "is not null":
                    builder.Append("!= null");
                    needValue = false;
                    break;
                case "=":
                    builder.Append(values.Count > 1 ? "in" : "=");
                    break;
                case "!=":
                    builder.Append(values.Count > 1 ? "not in" : "!=");
                    break;
                case "starts with":
                    builder.Append("like");
                    break;
                case "ends with":
                    builder.Append("like");
                    break;
                case "contains":
                    builder.Append(!query.PropertyIsCollection ? "like" : "contains");

                    break;
                default:
                    builder.Append(query.Operator);

                    break;
            }

            if (needValue)
            {
                builder.Append(" ");

                if (values.Count > 1)
                {
                    builder.Append("(");
                    builder.Append(string.Join(',', values.Select(v => ValueToSql(v))));
                    builder.Append(")");
                }
                else
                {
                    builder.Append(ValueToSql(values[0], query.Operator));
                }
            }
        }
    }

    private static void MetadataForScalarProperty(string property, List<JObject> result, QueryMetadata metadata)
    {
        List<string> values = new();
        var type = PropertyType.Unknown;
        var canBeNull = false;

        foreach (var jo in result)
        {
            var jt = (JProperty)jo.First!;

            var ct = jt.Value.Type switch
            {
                JTokenType.Integer => PropertyType.SomeInteger,
                JTokenType.Float => PropertyType.SomeFloat,
                JTokenType.String => PropertyType.String,
                JTokenType.Boolean => PropertyType.Boolean,
                JTokenType.Null => PropertyType.Null,

                JTokenType.Date => PropertyType.Date,
                JTokenType.Guid => PropertyType.String,
                JTokenType.Uri => PropertyType.String,
                JTokenType.TimeSpan => PropertyType.String,

                _ => throw new ArgumentOutOfRangeException()
            };

            // not initialized
            if (type == PropertyType.Unknown)
            {
                type = ct;
            }
            // float replace integers
            else if (type == PropertyType.SomeInteger && ct == PropertyType.SomeFloat)
            {
                type = ct;
            }
            // integers do not replace floats
            else if (type == PropertyType.SomeFloat && ct == PropertyType.SomeInteger)
            {
                // nothing to do
            }
            // a real type overrides null
            else if (type == PropertyType.Null && ct != PropertyType.Null)
            {
                type = ct;
            }
            // null does not override a concrete type
            else if (type != PropertyType.Null && ct == PropertyType.Null)
            {
                // nothing to do
            }
            // string overrides everything
            else if (ct == PropertyType.String)
            {
                type = ct;
            }
            // inconsistent type => error (this should never happen)
            else if (type != ct && ct != PropertyType.Null)
            {
                throw new NotSupportedException($"Inconsistent type for property {property}");
            }

            var jtk = jt.Value;
            string? val = null;


            if (jtk.Type == JTokenType.Date)
            {
                var date = jtk.Value<DateTime>();
                val = SmartDateTimeConverter.FormatDate(date);
            }
            else if (jtk.Type != JTokenType.Null)
            {
                val = jtk.Value<string>();
            }

            if (!string.IsNullOrEmpty(val))
                values.Add(val);
            else
                canBeNull = true;

            // no values list for float properties
            if (type != PropertyType.SomeFloat) metadata.PossibleValues = values.OrderBy(x => x).ToArray();


            metadata.PropertyType = type;

            // equal and different always accepted
            var operators = new List<string> { "=", "!=" };

            // comparison operators
            if (type is PropertyType.SomeFloat or PropertyType.SomeInteger or PropertyType.Date)
            {
                operators.Add(">");
                operators.Add(">=");
                operators.Add("<");
                operators.Add("<=");
            }

            // string operators for strings only
            if (type is PropertyType.String)
            {
                operators.Add("starts with");
                operators.Add("ends with");
                operators.Add("contains");
            }

            if (canBeNull)
            {
                operators.Add("is null");
                operators.Add("is not null");
            }

            metadata.AvailableOperators = operators.ToArray();
        }
    }

    private static void MetadataForCollectionProperty(string property, List<JObject> result, QueryMetadata metadata)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));
        metadata.PropertyIsCollection = true;

        var type = ((JProperty)result[0].First!)!.Value.Type;

        metadata.PropertyType = type switch
        {
            JTokenType.Boolean => PropertyType.Boolean,
            JTokenType.Date => PropertyType.Date,
            JTokenType.Float => PropertyType.SomeFloat,
            JTokenType.Integer => PropertyType.SomeInteger,
            _ => PropertyType.String
        };

        metadata.PossibleValues =
            result.Select(x => SmartDateTimeConverter.FormatDate((DateTime)((JProperty)x.First!)!.Value)).ToArray();

        // only "contains" and "not contains" for collections
        metadata.AvailableOperators = new[] { "contains", "not contains" };
    }
}