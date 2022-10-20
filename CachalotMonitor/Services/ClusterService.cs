using Cachalot.Linq;
using Client.Interface;
using System.Text;

namespace CachalotMonitor.Services
{
    public class ClusterService : IClusterService
    {
        public Connector? Connector { get; private set; }


        public string Connect(Model.ConnectionInfo connectionInfo)
        {

            if (connectionInfo.Nodes.Length == 0)
            {
                throw new ArgumentException("The connection info does not contain any node");
            }

            var cx = new StringBuilder();
            foreach (var node in connectionInfo.Nodes)
            {
                cx.Append(node.Host);
                cx.Append(':');
                cx.Append(node.Port);
                cx.Append('+');
            }

            var connectionString = cx.ToString().TrimEnd('+');

            Connector = new Connector(connectionString);

            // works like a high-level ping to check it is really connected
            Connector.GetClusterDescription();

            return connectionString;

        }

        public ClusterInformation GetClusterInformation()
        {
            if (Connector == null)
                throw new NotSupportedException("Not connected");

            return Connector.GetClusterDescription();
        }
    }
}
