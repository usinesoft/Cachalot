using System;
using System.Diagnostics;

namespace AdminConsole.Commands;

public class ConsoleProfiler
{
    private readonly Stopwatch _watch = new();
    private string _currentAction;

    public bool IsActive { get; set; } = false;


    public long TotalTimeMilliseconds => _watch.ElapsedMilliseconds;

    public void Start(string action)
    {
        _currentAction = action;

        _watch.Restart();
    }

    public void End()
    {
        _watch.Stop();

        if (IsActive) Console.WriteLine($"{_currentAction} took {_watch.ElapsedMilliseconds} ms");
    }
}