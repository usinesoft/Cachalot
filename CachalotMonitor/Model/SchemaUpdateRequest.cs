using Client.Core;

namespace CachalotMonitor.Model;

public record SchemaUpdateRequest(string CollectionName, string PropertyName, IndexType IndexType);