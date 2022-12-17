using Client.Core;
using Client.Interface;

namespace CachalotMonitor.Services;

/// <summary>
/// A long server process
/// </summary>
public class Process
{
    [ServerSideValue(IndexType.Primary)]
    public Guid ProcessId { get; set; }
    
    [ServerSideValue]
    public string? ProcessName { get; set; }

    [ServerSideValue]
    public string? ClusterName { get; set; }
    
    [ServerSideValue(IndexType.Dictionary)]
    public ProcessStatus Status { get; set; }

    [ServerSideValue(IndexType.Ordered)]
    public DateTime? StartTime { get; set; }
    
    public DateTime? EndTime { get; set; }

    public string? ErrorMessage { get; set; }
    public int DurationInSeconds
    {
        get
        {
            if (!StartTime.HasValue)
                return 0;

            var start = StartTime.Value;

            var end = EndTime ?? DateTime.Now;

            return (int)(end - start).TotalSeconds;
        }
    }


}