namespace CachalotMonitor.Services;

public interface IAdminService:IDisposable
{
    /// <summary>
    /// Set the backup directory for the current cluster
    /// </summary>
    /// <param name="backupDirectory"></param>
    void SetBackupDirectory(string backupDirectory);


    /// <summary>
    /// Get the last used backup directory for the current cluster
    /// </summary>
    /// <returns></returns>
    string? GetBackupDirectory();
    
    /// <summary>
    /// Get available backups for the current cluster
    /// </summary>
    /// <returns></returns>
    string[] GetAvailableBackups();

    Guid StartBackup();
    
    Guid StartRestore(string backup);

    Guid StartRecreate(string backup);

    Process? GetProcessInfo(Guid id);

    Process[] GetLastProcesses(int count);

    public void DropDatabase();

    public void TruncateTable(string collectionName);

    void DeleteProcess(Guid id);

    
}