using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Client.Core;

public class QueryExecutionPlan
{
    private readonly List<string> _traces = new();


    private readonly Stopwatch _watch = new();

    public QueryExecutionPlan(string query)
    {
        Query = query;
    }

    public string Query { get; set; }


    /// <summary>
    ///     If true, simplified execution for an atomic queries
    /// </summary>
    public bool SimpleQueryStrategy { get; set; }

    public bool FullScan { get; set; }

    /// <summary>
    ///     Time to choose the indexes
    /// </summary>
    public int PlanningTimeInMicroseconds { get; set; }

    /// <summary>
    ///     Time to process the indexes
    /// </summary>
    public int IndexTimeInMicroseconds { get; set; }

    /// <summary>
    ///     Time to process non-index-able part
    /// </summary>
    public int ScanTimeInMicroseconds { get; set; }

    public List<string> UsedIndexes { get; set; }

    public void Trace(string message)
    {
        _traces.Add(message);
    }


    public void StartPlanning()
    {
        _watch.Restart();
    }

    public void EndPlanning(List<string> usedIndexes)
    {
        _watch.Stop();

        PlanningTimeInMicroseconds = (int)(_watch.Elapsed.TotalMilliseconds * 1000D);

        UsedIndexes = usedIndexes;
    }


    public void StartIndexUse()
    {
        _watch.Restart();
    }

    public void EndIndexUse()
    {
        _watch.Stop();

        IndexTimeInMicroseconds = (int)(_watch.Elapsed.TotalMilliseconds * 1000D);
    }

    public void StartScan()
    {
        _watch.Restart();
    }

    public void EndScan()
    {
        _watch.Stop();

        ScanTimeInMicroseconds = (int)(_watch.Elapsed.TotalMilliseconds * 1000D);
    }


    public override string ToString()
    {
        var result = new StringBuilder();

        result.AppendLine(Query);

        result.AppendLine(
            $"{nameof(SimpleQueryStrategy)}: {SimpleQueryStrategy}, {nameof(FullScan)}: {FullScan}, PlanningTime: {PlanningTimeInMicroseconds} μs, IndexTime: {IndexTimeInMicroseconds} μs, ScanTime: {ScanTimeInMicroseconds} μs");

        foreach (var trace in _traces) result.AppendLine(trace);

        return result.ToString();
    }
}