namespace CachalotMonitor.Model;

public class ConnectionResponse
{
    public string? ConnectionString { get; set; }

    public string? ErrorMessage { get; set; }

    public bool? Success => ConnectionString != null;
}