namespace Server;

public class NodeConfig : INodeConfig
{
    public bool IsPersistent { get; set; }

    public int TcpPort { get; set; }

    public int MemoryLimitInGigabytes { get; set; }

    public string ClusterName { get; set; }

    public string DataPath { get; set; } = ".";

    public FullTextConfig FullTextConfig { get; set; }
}