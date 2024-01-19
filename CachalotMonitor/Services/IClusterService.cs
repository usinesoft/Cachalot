using Cachalot.Linq;
using CachalotMonitor.Model;
using Client.Interface;
using ConnectionInfo = CachalotMonitor.Model.ConnectionInfo;

namespace CachalotMonitor.Services;

public interface IClusterService : IDisposable
{
    Connector? Connector { get; }

    string Connect(ConnectionInfo connectionInfo);

    void Disconnect();

    ClusterInformation GetClusterInformation();

    void SaveToConnectionHistory(ConnectionInfo info, string name);

    ConnectionInfo GetFromConnectionHistory(string name);

    HistoryResponse GetHistoryEntries();
}