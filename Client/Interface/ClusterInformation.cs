using System.Linq;
using Client.Messages;

namespace Client.Interface
{
    public class ClusterInformation
    {
        public ClusterInformation(ServerDescriptionResponse[] serverDescriptions)
        {
            ServersStatus = serverDescriptions.Select(d => d.ServerProcessInfo).ToArray();
            Schema = serverDescriptions.First().KnownTypesByFullName.Values.ToArray();
        }

        public ServerInfo[] ServersStatus { get; }
        public TypeDescription[] Schema { get; }
    }
}