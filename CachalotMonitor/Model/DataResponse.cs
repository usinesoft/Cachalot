namespace CachalotMonitor.Model;

public class DataResponse
{
    public string? Json { get; set; }
    public string? Error { get; set; }

    public Guid QueryId { get; set; }

    public int ClientTimeInMilliseconds { get; set; }
}