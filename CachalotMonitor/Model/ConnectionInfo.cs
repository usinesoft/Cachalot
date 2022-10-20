namespace CachalotMonitor.Model
{
    public class ConnectionInfo
    {
        public string? ClusterName { get; set; }

        public ClusterNode[] Nodes { get; set; } = new ClusterNode[0];
    }

    public class ClusterNode
    {

        public string? Host { get; set; }
    
        public int? Port { get; set; }
    }

    public class ConnectionResponse
    {
        public string? ConnectionString  { get; set; }
        
        public string? ErrorMessage  { get; set; }
        
        public bool? Success => ConnectionString != null;

    }
}
