using System.Text;
using Cachalot.Linq;
using CachalotMonitor.Model;
using Client.Interface;
using Client.Messages;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ConnectionInfo = CachalotMonitor.Model.ConnectionInfo;

namespace CachalotMonitor.Services;

public class ClusterService : IClusterService
{
    private const string HistoryPath = "history";

    private const string HistoryExtension = "cnx";

    private readonly Dictionary<string, ConnectionInfo> _connectionHistoryCache = new();

    private readonly ILogger<ClusterService> _logger;

    private readonly ShowcaseConfig _showcaseConfig;

    public ClusterService(ILogger<ClusterService> logger, IOptions<ShowcaseConfig> showcaseOptions)
    {
        _logger = logger;

        _showcaseConfig = showcaseOptions.Value;

        LoadConnectionHistory();
    }

    public string? ConnectionString { get; private set; }

    public Connector? Connector { get; private set; }

    public string Connect(ConnectionInfo connectionInfo)
    {
        if (connectionInfo.Nodes.Length == 0)
            throw new ArgumentException("The connection info does not contain any node");

        var cx = new StringBuilder();
        foreach (var node in connectionInfo.Nodes)
        {
            cx.Append(node.Host);
            cx.Append(':');
            cx.Append(node.Port);
            cx.Append('+');
        }

        var newCxString  = cx.ToString().TrimEnd('+');

        if (newCxString != ConnectionString)
        {
            ConnectionString = newCxString;

            Connector?.Dispose();

            Connector = new(ConnectionString);
            
        }
        
        return ConnectionString;
    }

    public void Disconnect()
    {
        // do nothing
        // the connector is disposed only when we connect with a different connection string

    }

    public ClusterInformation GetClusterInformation()
    {
        if (Connector == null)
            return new(Array.Empty<ServerDescriptionResponse>());

        return Connector.GetClusterDescription();
    }

    public void SaveToConnectionHistory(ConnectionInfo info, string name)
    {
        var historyPath = Path.Combine(Directory.GetCurrentDirectory(), HistoryPath);

        if (!Directory.Exists(historyPath)) Directory.CreateDirectory(historyPath);

        var json = JsonConvert.SerializeObject(info);

        var filePath = Path.Combine(historyPath, $"{name}.{HistoryExtension}");

        File.WriteAllText(filePath, json);

        _connectionHistoryCache[name] = info;
    }

    public ConnectionInfo GetFromConnectionHistory(string name)
    {
        return _connectionHistoryCache[name];
    }

    public HistoryResponse GetHistoryEntries()
    {
        return new(_showcaseConfig.ShowcaseMode, _connectionHistoryCache.Keys.ToArray());
    }

    public void Dispose()
    {
        Connector?.Dispose();
    }

    private void LoadConnectionHistory()
    {
        var historyPath = Path.Combine(Directory.GetCurrentDirectory(), HistoryPath);

        if (!Directory.Exists(historyPath)) return;

        var files = Directory.EnumerateFiles(historyPath, $"*.{HistoryExtension}");
        foreach (var file in files)
        {
            var json = File.ReadAllText(file);
            var info = JsonConvert.DeserializeObject<ConnectionInfo>(json);
            if (info != null) _connectionHistoryCache[Path.GetFileNameWithoutExtension(file)] = info;
        }
    }
}