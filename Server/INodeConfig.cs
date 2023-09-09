namespace Server;

public interface INodeConfig
{
    bool IsPersistent { get; set; }

    int TcpPort { get; set; }

    public int MemoryLimitInGigabytes { get; }

    string ClusterName { get; set; }

    string DataPath { get; set; }

    FullTextConfig FullTextConfig { get; set; }
}