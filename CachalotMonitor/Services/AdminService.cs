using Cachalot.Linq;
using Client.Interface;
using Newtonsoft.Json;
using System.IO;

namespace CachalotMonitor.Services;

public class AdminService : IAdminService
{
    private readonly IClusterService _clusterService;

    /// <summary>
    /// Backup directory by cluster
    /// </summary>
    readonly Dictionary<string, string> _settings = new();

    private const string AdminConfig = "admin.json";

    // an in-process database used to store the processing history
    private readonly Connector _adminDatabase;

    public AdminService(ILogger<IAdminService> logger, IClusterService clusterService)
    {
        _clusterService = clusterService;

        if (File.Exists(AdminConfig))
        {
            var json = File.ReadAllText(AdminConfig);
            if (!string.IsNullOrWhiteSpace(json))
            {
                _settings = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)!;
            }
            
        }

        _adminDatabase = new Connector(true); // persistent in-process

        _adminDatabase.DeclareCollection<Process>();

        ProcessCollection = _adminDatabase.DataSource<Process>();
    }

    public DataSource<Process> ProcessCollection { get; set; }

    /// <summary>
    /// Remember the backup directory for each cluster between sessions
    /// </summary>
    /// <param name="backupDirectory"></param>
    public void SetBackupDirectory(string backupDirectory)
    {
        if (!Directory.Exists(backupDirectory))
            return;

        var clusterName = ClusterName();

        if (clusterName != null)
        {
            _settings[clusterName] = backupDirectory;

            var json = JsonConvert.SerializeObject(_settings);
            File.WriteAllText(AdminConfig, json);
        }
        
        
    }

    private string? ClusterName()
    {
        var info = _clusterService.GetClusterInformation();
        if (info.Status == ClusterInformation.ClusterStatus.Ok)
        {
            return info.ServersStatus[0].ClusterName;
        }

        return null;
    }

    public string? GetBackupDirectory()
    {
        var clusterName = ClusterName();

        if (clusterName != null && _settings.TryGetValue(clusterName, out var dir))
        {
            return dir;
        }

        return null;
    }

    public string[] GetAvailableBackups()
    {
        var dir = GetBackupDirectory();
        if (dir != null && Directory.Exists(dir))
        {
            //example of dump directory:2022-12-11_09h44_01nodes_test 
            var dirs = Directory.EnumerateDirectories(dir, "????-??-??_??h??_*", SearchOption.TopDirectoryOnly).ToList();
            return dirs.Select(x => x.Trim()).Select(x=> new DirectoryInfo(x).Name).OrderByDescending(x=>x).ToArray()!;
        }
        
        return Array.Empty<string>();
    }


    public Guid BackgroundProcess(string processName, Action action)
    {
        var id = Guid.NewGuid();
        var clusterName = ClusterName();

        var process = new Process
        {
            ProcessId = id,
            StartTime = DateTime.Now,
            ProcessName = processName,
            Status = ProcessStatus.Running,
            ClusterName = clusterName
        };

        ProcessCollection.Put(process);

        Task.Run(() =>
        {
            try
            {
                action();
                
                process.Status = ProcessStatus.Success;
            }
            catch (Exception e)
            {
                process.Status = ProcessStatus.Failed;
                process.ErrorMessage = e.Message;
            }
            finally
            {
                process.EndTime= DateTime.Now;
                ProcessCollection.Put(process);
            }

        });


        return id;
    }

    public Guid StartBackup()
    {
        var backupDirectory = GetBackupDirectory();
        
        if(backupDirectory == null) return Guid.Empty;
        
        return BackgroundProcess("backup", () => _clusterService.Connector?.AdminInterface().Dump(backupDirectory));
        
    }

    public Guid StartRestore(string backup)
    {
        var backupDirectory = GetBackupDirectory();
        
        if(backupDirectory == null) return Guid.Empty;

        var fullPath = Path.Combine(backupDirectory, backup);

        return BackgroundProcess("restore", () => _clusterService.Connector?.AdminInterface().ImportDump(fullPath));
    }

    public Guid StartRecreate(string backup)
    {
        var backupDirectory = GetBackupDirectory();
        
        if(backupDirectory == null) return Guid.Empty;

        var fullPath = Path.Combine(backupDirectory, backup);

        return BackgroundProcess("recreate", () => _clusterService.Connector?.AdminInterface().InitializeFromDump(fullPath));
    }

    public Process? GetProcessInfo(Guid id)
    {
        return ProcessCollection[id];
    }

    public Process[] GetLastProcesses(int count)
    {
        return ProcessCollection.OrderByDescending(x=>x.StartTime).Take(count).ToArray();
    }

    public void DropDatabase()
    {
        _clusterService.Connector?.AdminInterface().DropDatabase();
    }

    public void TruncateTable(string collectionName)
    {
        _clusterService.Connector?.Truncate(collectionName);
    }

    public void DeleteProcess(Guid id)
    {
        var toDelete = ProcessCollection[id];
        if (toDelete != null)
        {
            ProcessCollection.Delete(toDelete);
        }
        
    }

    public void Dispose()
    {
        _adminDatabase.Dispose();
    }
}