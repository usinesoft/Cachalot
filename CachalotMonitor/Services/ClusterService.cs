using Cachalot.Linq;
using Client.Interface;
using System.Text;
using Client.Messages;
using Newtonsoft.Json;

namespace CachalotMonitor.Services
{
    public class ClusterService : IClusterService
    {
        public ClusterService(ILogger<ClusterService> logger)
        {
            _logger = logger;

            LoadConnectionHistory();
        }

        public Connector? Connector { get; private set; }

        private readonly ILogger<ClusterService> _logger;

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

            Connector?.Dispose();

            Connector = new Connector(connectionString);

            return connectionString;

        }

        public void Disconnect()
        {
            Connector?.Dispose();
            Connector = null;
        }

        public ClusterInformation GetClusterInformation()
        {
            if (Connector == null)
                return new ClusterInformation(Array.Empty<ServerDescriptionResponse>());

            return Connector.GetClusterDescription();
        }

        private readonly Dictionary<string, Model.ConnectionInfo> _connectionHistoryCache = new();

        private const string HistoryPath = "history";
        
        private const string HistoryExtension = "cnx"; 

        public void SaveToConnectionHistory(Model.ConnectionInfo info, string name)
        {
            var historyPath = Path.Combine(Directory.GetCurrentDirectory(), HistoryPath);

            if (!Directory.Exists(historyPath))
            {
                Directory.CreateDirectory(historyPath);
            }
            
            var json = JsonConvert.SerializeObject(info);

            var filePath = Path.Combine(historyPath, $"{name}.{HistoryExtension}");

            File.WriteAllText(filePath, json);

            _connectionHistoryCache[name] = info;
        }

        public Model.ConnectionInfo GetFromConnectionHistory(string name)
        {
            return _connectionHistoryCache[name];
        }

        public string[] GetHistoryEntries()
        {
            return _connectionHistoryCache.Keys.ToArray(); 
        }

        private void LoadConnectionHistory()
        {
            var historyPath = Path.Combine(Directory.GetCurrentDirectory(), HistoryPath);

            if (!Directory.Exists(historyPath))
            {
                return;
            }

            var files = Directory.EnumerateFiles(historyPath,$"*.{HistoryExtension}");
            foreach (var file in files)
            {
                var json = File.ReadAllText(file);
                var info = JsonConvert.DeserializeObject<Model.ConnectionInfo>(json);
                if (info != null)
                {
                    _connectionHistoryCache[Path.GetFileNameWithoutExtension(file)] = info;
                }
                
            }

        }

        public void Dispose()
        {
            Connector?.Dispose();
        }
    }
}
