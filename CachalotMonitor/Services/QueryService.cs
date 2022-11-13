using CachalotMonitor.Model;
using Newtonsoft.Json.Linq;

namespace CachalotMonitor.Services;

class QueryService : IQueryService
{
    readonly IClusterService _clusterService;

    public QueryService(IClusterService clusterService)
    {
        _clusterService = clusterService;
    }

    public QueryMetadata GetMetadata(string collection, string property)
    {
        try
        {
            var info = _clusterService.GetClusterInformation();
            
            var metadata = new QueryMetadata();

            var schema = info.Schema.FirstOrDefault(x => x.CollectionName.Equals(collection,StringComparison.CurrentCultureIgnoreCase));

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
                    var result = _clusterService.Connector?.SqlQueryAsJson($"select distinct {property} from {collection} take 1000").ToList();

                    if (result == null || result.Count == 0)
                    {
                        metadata.AvailableOperators = new[] {"is null", "is not null" };
                        return metadata;
                    }

                    if (pr.IsCollection)
                    {
                        MetadataForCollectionProperty(property, result, metadata);    
                    }
                    else
                    {
                        MetadataForScalarProperty(property, result, metadata);    
                    }
                    

                    return metadata;
                }
            }
        }
        catch (Exception)
        {
            return new QueryMetadata { Found = false };
        }

        return new QueryMetadata { Found = false };

    }

    private static void MetadataForScalarProperty(string property, List<JObject> result, QueryMetadata metadata)
    {
        List<string> values = new();
        var type = PropertyType.Unknown;
        var canBeNull = false;

        foreach (var jo in result)
        {
            var jt = (JProperty)jo.First!;

            var ct = jt!.Value.Type switch
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

            var val = jt.Value.Value<string>();
            if (!string.IsNullOrEmpty(val))
            {
                values.Add(val);
            }
            else
            {
                canBeNull = true;
            }

            // no values list for float properties
            if (type != PropertyType.SomeFloat)
            {
                metadata.PossibleValues = values.OrderBy(x=>x).ToArray();
            }
            
            
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
            if(type is PropertyType.String)
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
        metadata.PropertyIsCollection = true;

        metadata.PropertyType = PropertyType.String; // does not really matter for collections
    
        // only "contains" and "not contains" for collections

        metadata.AvailableOperators = new[] { "contains", "not contains" };
        
        
    }
}