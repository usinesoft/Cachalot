using CachalotMonitor.Model;
using CachalotMonitor.Services;
using Client.Interface;
using Microsoft.AspNetCore.Mvc;
using ConnectionInfo = CachalotMonitor.Model.ConnectionInfo;

namespace CachalotMonitor.Controllers;

[Route("[controller]")]
[ApiController]
public class AdminController : ControllerBase
{
    public AdminController(IClusterService clusterService, IAdminService adminService, ISchemaService schemaService, IAuthenticationService authService)
    {
        ClusterService = clusterService;
        AdminService = adminService;
        SchemaService = schemaService;
        AuthService = authService;
    }

    private IClusterService ClusterService { get; }
    public IAdminService AdminService { get; }

    public ISchemaService SchemaService { get; }
    public IAuthenticationService AuthService { get; }

    /// <summary>
    ///     Connect with explicit connection data
    /// </summary>
    /// <param name="connectionInfo"></param>
    /// <returns></returns>
    [HttpPost("connect")]
    public ConnectionResponse Connect([FromBody] ConnectionInfo connectionInfo)
    {
        try
        {
            var connectionString = ClusterService.Connect(connectionInfo);

            var clusterInfo = GetClusterInformation();

            if (clusterInfo.Status == ClusterInformation.ClusterStatus.Ok)
            {
                ClusterService.SaveToConnectionHistory(connectionInfo, clusterInfo.ServersStatus[0].ClusterName);
                return new() { ConnectionString = connectionString };
            }

            return new() { ErrorMessage = clusterInfo.StatusReason };
        }
        catch (Exception ex)
        {
            return new() { ErrorMessage = ex.Message };
        }
    }

    [HttpPost("update/schema")]
    public void UpdateSchema([FromBody] SchemaUpdateRequest request)
    {
        SchemaService.UpdateSchema(request);
    }


    /// <summary>
    ///     Connect to a cluster from the connection history
    /// </summary>
    /// <param name="clusterName"></param>
    /// <returns></returns>
    [HttpPost("connect/{clusterName}")]
    public ConnectionResponse ConnectWithHistory(string clusterName)
    {
        try
        {
            var info = ClusterService.GetFromConnectionHistory(clusterName);

            var connectionString = ClusterService.Connect(info);

            // works like a high level ping
            var clusterInfo = GetClusterInformation();
            if (clusterInfo.Status == ClusterInformation.ClusterStatus.Ok)
                return new() { ConnectionString = connectionString };

            return new() { ErrorMessage = clusterInfo.StatusReason };
        }
        catch (Exception ex)
        {
            return new() { ErrorMessage = ex.Message };
        }
    }


    /// <summary>
    ///     Disconnect from current cluster
    /// </summary>
    /// <returns></returns>
    [HttpPost("disconnect")]
    public void Disconnect()
    {
        ClusterService.Disconnect();
    }

    /// <summary>
    ///     Get history of successful connections
    /// </summary>
    /// <returns></returns>
    [HttpGet("history")]
    public HistoryResponse GetConnectionHistory()
    {
        return ClusterService.GetHistoryEntries();
    }


    /// <summary>
    ///     Get information about cluster nodes and collections
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public ClusterInformation GetClusterInformation()
    {
        return ClusterService.GetClusterInformation();
    }

    [HttpPost("backup/path")]
    [AuthenticationFilter]
    public void ConfigureBackup([FromBody] BackupConfig cfg)
    {
        if (cfg.BackupDirectory != null)
            AdminService.SetBackupDirectory(cfg.BackupDirectory);
    }

    [HttpGet("backup/path")]
    public BackupConfig GetBackupDirectory()
    {
        return new(AdminService.GetBackupDirectory());
    }

    [HttpGet("backup/list")]
    public string[] GetAvailableBackups()
    {
        return AdminService.GetAvailableBackups().OrderByDescending(x => x).ToArray();
    }

    [HttpPost("backup/save")]
    public void CreateBackup()
    {
        AdminService.StartBackup();
    }

    [HttpPost("backup/restore/{backup}")]
    [AuthenticationFilter]
    public void RestoreFromBackup(string backup)
    {
        AdminService.StartRestore(backup);
    }

    [HttpPost("backup/recreate/{backup}")]
    [AuthenticationFilter]
    public void RecreateFromBackup(string backup)
    {
        AdminService.StartRecreate(backup);
    }

    [HttpGet("process/list")]
    public Process[] GetProcessHistory()
    {
        return AdminService.GetLastProcesses(20).OrderByDescending(x => x.StartTime).ToArray();
    }

    [HttpDelete("process/delete/{id}")]
    [AuthenticationFilter]
    public void DeleteProcess(Guid id)
    {
        AdminService.DeleteProcess(id);
    }


    [HttpDelete("truncate/{collection}")]
    [AuthenticationFilter]
    public void Truncate(string collection)
    {
        AdminService.TruncateTable(collection);
    }

    [HttpDelete("drop/{collection}")]
    [AuthenticationFilter]
    public void DropCollection(string collection)
    {
        ClusterService.Connector?.DropCollection(collection);
    }

    [HttpDelete("drop")]
    [AuthenticationFilter]
    public void Drop()
    {
        AdminService.DropDatabase();
    }

    [HttpGet("check-code/{code}")]
    public CodeValidationResponse CheckCode(string code)
    {
        var result = AuthService.CheckAdminCode(code);

        return new CodeValidationResponse { IsValid = result };
    }

    [HttpPost("read-only")]
    [AuthenticationFilter]
    public void ReadOnly()
    {
        AdminService.SwitchToReadOnly();
    }

    [HttpPost("read-write")]
    [AuthenticationFilter]
    public void ReadWrite()
    {
        AdminService.SwitchToReadWrite();
    }
}