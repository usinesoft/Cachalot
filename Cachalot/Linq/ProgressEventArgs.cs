using System;

namespace Cachalot.Linq;

public class ProgressEventArgs : EventArgs
{
    public enum ProgressNotification
    {
        Start,
        Progress,
        End
    }

    public ProgressEventArgs(ProgressNotification type, int itemsProcessed, int totalToProcess = 0)
    {
        Type = type;
        ItemsProcessed = itemsProcessed;
        TotalToProcess = totalToProcess;
    }

    public int ItemsProcessed { get; }

    public int TotalToProcess { get; }

    public ProgressNotification Type { get; }
}