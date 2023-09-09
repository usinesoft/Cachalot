#region

using System.Collections.Generic;
using Client;
using Client.Messages;

#endregion

namespace Server;

public static class ServerLog
{
    private const int MaxEntries = 1000;
    private static readonly LinkedList<ServerLogEntry> Entries;
    private static readonly object SyncRoot;

    static ServerLog()
    {
        Entries = new();
        SyncRoot = new();
    }

    public static ILog ExternalLog { private get; set; }

    public static ServerLogEntry MaxLogEntry { get; set; }

    public static void AddEntry(ServerLogEntry newEntry)
    {
        lock (SyncRoot)
        {
            Entries.AddLast(newEntry);
            if (Entries.Count > MaxEntries)
                Entries.RemoveFirst();

            if (MaxLogEntry == null)
                MaxLogEntry = newEntry;
            else if (MaxLogEntry.CacheAccessTimeInMilliseconds < newEntry.CacheAccessTimeInMilliseconds)
                MaxLogEntry = newEntry;
        }

        ExternalLog?.LogInfo(newEntry.ToString());
    }

    public static IList<ServerLogEntry> GetLast(int count)
    {
        if (count > MaxEntries)
            count = MaxEntries;

        var result = new List<ServerLogEntry>(count);
        lock (SyncRoot)
        {
            if (count > Entries.Count)
                count = Entries.Count;

            var entries = 0;

            var curEntry = Entries.Last;
            while (entries < count)
            {
                result.Add(curEntry.Value);
                curEntry = curEntry.Previous;
                entries++;
            }
        }

        return result;
    }

    public static void LogDebug(string message)
    {
        ExternalLog?.LogDebug(message);
    }

    public static void LogInfo(string message)
    {
        Dbg.Trace(message);
        ExternalLog?.LogInfo(message);
    }

    public static void LogWarning(string message)
    {
        ExternalLog?.LogWarning(message);
    }

    public static void LogError(string message)
    {
        ExternalLog?.LogError(message);
    }
}