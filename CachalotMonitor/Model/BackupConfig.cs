namespace CachalotMonitor.Model;

public record BackupConfig(string? BackupDirectory);

public class ShowcaseConfig
{
    
    public bool ShowcaseMode { get; set; }
    
}

public record HistoryResponse(bool ShowcaseMode, IReadOnlyCollection<string> KnownClusters);