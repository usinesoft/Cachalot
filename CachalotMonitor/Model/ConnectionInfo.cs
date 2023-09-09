namespace CachalotMonitor.Model;

public class ConnectionInfo
{
    public string? ClusterName { get; set; }

    public ClusterNode[] Nodes { get; set; } = Array.Empty<ClusterNode>();
}