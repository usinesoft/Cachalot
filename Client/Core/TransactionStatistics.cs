using System;
using System.Threading;

namespace Client.Core;

/// <summary>
///     Used to fine-tune the transaction mechanism
/// </summary>
public static class TransactionStatistics
{
    private static long _transactionsOk;

    private static long _totalAttemptsToLock;

    private static long _maxRetriesBeforeLock;

    private static readonly object SyncRoot = new();


    private static long _executedAsSingleStage;

    public static void NewTransactionCompleted()
    {
        Interlocked.Increment(ref _transactionsOk);
    }

    public static void NewAttemptToLock()
    {
        Interlocked.Increment(ref _totalAttemptsToLock);
    }

    public static void Retries(int retries)
    {
        lock (SyncRoot)
        {
            if (retries > _maxRetriesBeforeLock) _maxRetriesBeforeLock = retries;
        }
    }

    public static string AsString()
    {
        return
            $"max retries:{_maxRetriesBeforeLock} total completed:{_transactionsOk} total retries:{_totalAttemptsToLock} as single stage:{_executedAsSingleStage}";
    }

    public static void Display()
    {
        Console.WriteLine(AsString());
    }

    public static void ExecutedAsSingleStage()
    {
        Interlocked.Increment(ref _executedAsSingleStage);
    }

    public static void Reset()
    {
        _transactionsOk = 0;
        _maxRetriesBeforeLock = 0;
        _executedAsSingleStage = 0;
        _totalAttemptsToLock = 0;
    }
}