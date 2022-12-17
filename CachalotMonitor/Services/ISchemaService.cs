using CachalotMonitor.Model;

namespace CachalotMonitor.Services;

public interface ISchemaService
{
    void UpdateSchema(SchemaUpdateRequest updateRequest);
}