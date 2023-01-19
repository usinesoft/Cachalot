using Client.Core;

namespace CachalotMonitor.Model;

public class SqlResponse
{
    public string? Sql { get; set; }
    public string? Error { get; set; }
}

public class DataResponse
{
    public string? Json { get; set; }
    public string? Error { get; set; }
    
    public Guid QueryId { get; set; }

    public int ClientTimeInMilliseconds { get; set; }
}