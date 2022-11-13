using CachalotMonitor.Model;

namespace CachalotMonitor.Services;

public interface IQueryService
{
    public QueryMetadata GetMetadata(string collection, string property);
}

/// <summary>
/// Metadata to assist the graphical creation of a query for a property in a collection
/// </summary>
public class QueryMetadata
{
    public string? CollectionName { get; set; }
    
    public string? PropertyName { get; set; }

    public bool Found { get; set; }

    public PropertyType PropertyType { get; set; }

    public bool PropertyIsCollection { get; set; }

    public string[] PossibleValues { get; set; } = Array.Empty<string>();
    
    public string[] AvailableOperators { get; set; } = Array.Empty<string>();
}