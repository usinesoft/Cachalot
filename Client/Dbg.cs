using System;
using System.Diagnostics;
using System.Threading;
using JetBrains.Annotations;


// ReSharper disable once CheckNamespace
namespace Client;

public static class Dbg
{
    private static int _simulatedException;
    private static int _shardToFail = -1;

    private static readonly ConsoleColor NormalColor = Console.ForegroundColor;

    [Conditional("DEBUG_VERBOSE")]
    public static void Trace(string message, bool important = false)
    {
        var totalThreads = Process.GetCurrentProcess().Threads.Count;
        var msg =
            $"{DateTime.Now:hh:mm:ss.fff} thread {Thread.CurrentThread.ManagedThreadId:D3} / {totalThreads:D3}  {message}";

        if (message.ToLower().StartsWith("end ") || message.ToLower().StartsWith("stop ")) Debug.Unindent();

        Debug.WriteLine(msg);

        var indent = new string(' ', Debug.IndentLevel * 4);

        if (important)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(indent + msg);
            Console.ForegroundColor = NormalColor;
        }
        else
        {
            Console.ForegroundColor = NormalColor;
            Console.WriteLine(indent + msg);
        }
        

        if (message.ToLower().StartsWith("start ") || message.ToLower().StartsWith("begin ")) Debug.Indent();        
    }

    [Conditional("DEBUG_VERBOSE")]
    [AssertionMethod]
    public static void CheckThat([AssertionCondition(AssertionConditionType.IS_TRUE)] bool test, string message = null)
    {
        if (!test)
        {
            if (message == null)
                Debug.Assert(test);
            else
                Debug.Assert(test, message);
        }
    }

    [Conditional("DEBUG")]
    public static void SimulateException(int faultType, int shardIndex = -1)
    {
        if (faultType <= 0) throw new ArgumentOutOfRangeException(nameof(faultType));


        if (_simulatedException == faultType && (shardIndex == _shardToFail || _shardToFail == -1))
            throw new NotSupportedException("simulation" + faultType);
    }

    public static void ActivateSimulation(int faultType, int shardIndex = -1)
    {
        _simulatedException = faultType;
        _shardToFail = shardIndex;
    }

    public static void DeactivateSimulation()
    {
        _simulatedException = 0;
        _shardToFail = -1; // all
    }
}