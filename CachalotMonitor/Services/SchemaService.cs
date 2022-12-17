using CachalotMonitor.Model;

namespace CachalotMonitor.Services;

class SchemaService : ISchemaService
{
    private readonly IClusterService _clusterService;

    public SchemaService(IClusterService clusterService)
    {
        _clusterService = clusterService;
    }

    public void UpdateSchema(SchemaUpdateRequest updateRequest)
    {
        var collectionSchema = _clusterService.Connector?.GetCollectionSchema(updateRequest.CollectionName);

        if (collectionSchema != null)
        {
            var property = collectionSchema.ServerSide.FirstOrDefault(x => x.Name == updateRequest.PropertyName);
            if (property != null)
            {
                property.IndexType = updateRequest.IndexType;

                _clusterService.Connector!.DeclareCollection(updateRequest.CollectionName, collectionSchema);
            }
        }
    }
}