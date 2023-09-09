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

public interface INodeConfig
{
    bool IsPersistent { get; set; }

    int TcpPort { get; set; }

    public int MemoryLimitInGigabytes { get; }

    string ClusterName { get; set; }

    string DataPath { get; set; }

    FullTextConfig FullTextConfig { get; set; }
}