using System.Collections.Generic;

namespace Server
{
    public class NodeConfig
    {
        public bool IsPersistent { get; set; }

        public int TcpPort { get; set; }

        public string ClusterName { get; set; }

        public string DataPath { get; set; }

        public FullTextConfig FullTextConfig { get; set; }
    }


    public class FullTextConfig
    {
        public int MaxIndexedTokens { get; set; } = 10_000_000;

        public int MaxTokensToIgnore { get; set; } = 100;

        public  List<string> TokensToIgnore { get; set; } = new List<string>();
    }
}
