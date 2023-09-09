namespace CachalotMonitor.Model;

public class ConnectionInfo
{
    public string? ClusterName { get; set; }

    public ClusterNode[] Nodes { get; set; } = Array.Empty<ClusterNode>();
}

public class ClusterNode
{
    public string? Host { get; set; }

    public int? Port { get; set; }
}

public record BackupConfig(string? BackupDirectory);