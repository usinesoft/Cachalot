using Cachalot.Linq;
using Client.Interface;

namespace CachalotMonitor.Services
{
    public interface IClusterService
    {
        Connector? Connector { get; }

        string Connect(Model.ConnectionInfo connectionInfo);

        void Disconnect();

        ClusterInformation GetClusterInformation();
        void SaveToConnectionHistory(Model.ConnectionInfo info, string name);
        Model.ConnectionInfo GetFromConnectionHistory(string name);
        string[] GetHistoryEntries();
    }
}