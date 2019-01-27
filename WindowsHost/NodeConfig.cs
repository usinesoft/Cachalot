using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Host
{
    public class NodeConfig
    {
        public bool IsPersistent { get; set; }

        public int TcpPort { get; set; }

        public string ClusterName { get; set; }
    }
}
