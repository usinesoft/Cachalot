﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Client.Core;

namespace Server.HostServices.Logger;

/// <summary>
///     A very fast async logger
/// </summary>
internal class FastLogger : ILog
{
    private static readonly int MaxMessagesInQueue = 1000;

    private static readonly int MaxMessagesInCache = 1000;

    private static readonly int DelayBetweenWritesInMilliseconds = 1000;

    private static readonly int MaxFilesToKeep = 2;

    private readonly Queue<string> _logCache = new(MaxMessagesInCache);

    private readonly Queue<Item> _messageQueue = new(MaxMessagesInQueue);

    private readonly ManualResetEvent _wakeConsumerEvent = new(false);

    private string _logDirectory = "logs";

    private volatile bool _shouldStop;
    private Thread _worker;
    


    public DataStore ActivityTable { get; set; }

    public void LogActivity(string type, string collectionName, int executionTimeInMicroseconds, string detail,
                            string query = null, ExecutionPlan plan = null, Guid queryId = default)
    {
        // log in the file
        Log(LogLevel.Info, $"{detail} took {executionTimeInMicroseconds} μs");

        // log in the special @ACTIVITY table unless the log concerns the @ACTIVITY table itself
        if (collectionName == LogEntry.Table)
            return;

        lock (_messageQueue)
        {
            if (_messageQueue.Count < MaxMessagesInQueue)
                _messageQueue.Enqueue(new(collectionName, type, executionTimeInMicroseconds, detail, query, plan,
                    queryId));
        }
    }

    public void LogDebug(string message)
    {
        Log(LogLevel.Debug, message);
    }

    public void LogInfo(string message)
    {
        Log(LogLevel.Info, message);
    }

    public void LogWarning(string message)
    {
        Log(LogLevel.Warn, message);
    }

    public void LogError(string message)
    {
        Log(LogLevel.Error, message);
    }

    private void Log(LogLevel level, string message)
    {
        lock (_messageQueue)
        {
            if (_messageQueue.Count < MaxMessagesInQueue) _messageQueue.Enqueue(new(level, message));
        }
    }

    private void StoreInCache(string message)
    {
        lock (_logCache)
        {
            if (_logCache.Count > MaxMessagesInQueue) _logCache.Dequeue();

            _logCache.Enqueue(message);
        }
    }


    /// <summary>
    ///     Delete older log files
    /// </summary>
    private void DoHouseKeeping()
    {
        var logFiles = Directory.EnumerateFiles(_logDirectory, "*.log").OrderBy(n => n).ToList();
        if (logFiles.Count > MaxFilesToKeep)
            for (var i = 0; i < logFiles.Count - MaxFilesToKeep; i++)
                File.Delete(logFiles[i]);
    }

    public void Start(string path)
    {
        _logDirectory = path != null ? Path.Combine(path, "logs") : "logs";

        if (!Directory.Exists(_logDirectory)) Directory.CreateDirectory(_logDirectory);


        // initialize the @ACTIVITY table
        var schema = TypedSchemaFactory.FromType<LogEntry>();
        schema.CollectionName = LogEntry.Table;
        schema.StorageLayout = Layout.Compressed;

        ActivityTable = new(schema, new LruEvictionPolicy(20_000, 1000), new());


        _worker = new(() =>
        {
            while (!_shouldStop)
            {
                // wake up every second
                _wakeConsumerEvent.WaitOne(DelayBetweenWritesInMilliseconds);

                DoHouseKeeping();


                var newItems = new List<Item>();

                lock (_messageQueue)
                {
                    while (_messageQueue.Count > 0)
                    {
                        var msg = _messageQueue.Dequeue();

                        newItems.Add(msg);
                    }
                }

                if (newItems.Any())
                {
                    var fileName = DateTime.Now.ToString("yyyy-MM-dd") + ".log";



                    StreamWriter writer = new(Path.Combine(_logDirectory, fileName), true);

                    foreach (var item in newItems.Where(i => i.Entry == null))
                    {
                        writer.WriteLine(item);

                        StoreInCache(item.ToString());

                        Console.WriteLine(item);
                    }

                    writer.Dispose();

                    var entries = new List<PackedObject>();
                    foreach (var item in newItems.Where(i => i.Entry != null))
                        entries.Add(PackedObject.Pack(item.Entry, schema, LogEntry.Table));

                    if (entries.Any()) ActivityTable.InternalPutMany(entries, false);
                }
            }
        });

        _worker.Start();
    }


    public void Stop()
    {
        _shouldStop = true;

        _wakeConsumerEvent.Set();

        _worker.Join(1000);
    }

    public IList<string> GetCachedLog()
    {
        lock (_logCache)
        {
            return new List<string>(_logCache);
        }
    }

    private enum LogLevel
    {
        Debug,
        Info,
        Warn,
        Error
    }

    private sealed class Item
    {
        /// <summary>
        ///     free format log item
        /// </summary>
        /// <param name="logLevel"></param>
        /// <param name="message"></param>
        public Item(LogLevel logLevel, string message)
        {
            LogLevel = logLevel;
            Message = message;

            TimeStamp = DateTime.Now;
        }

        /// <summary>
        ///     Server activity log entry
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="type"></param>
        /// <param name="executionTimeInMicroseconds"></param>
        /// <param name="detail">sql-like description</param>
        /// <param name="query">query without parameters value</param>
        /// <param name="plan"></param>
        /// <param name="queryId">optional unique id used to find a query in the activity log</param>
        public Item(string collection, string type, int executionTimeInMicroseconds, string detail, string query,
                    ExecutionPlan plan = null, Guid queryId = default)
        {
            Entry = new()
            {
                Id = queryId == Guid.Empty ? Guid.NewGuid() : queryId,
                ExecutionPlan = plan,
                Type = type,
                Detail = detail,
                CollectionName = collection.ToLowerInvariant(),
                ExecutionTimeInMicroseconds = executionTimeInMicroseconds,
                Query = query,
                TimeStamp = DateTimeOffset.Now
            };
        }

        public LogEntry Entry { get; }

        private DateTime TimeStamp { get; }

        private LogLevel LogLevel { get; }

        private string Message { get; }

        public override string ToString()
        {
            var level = "???";
            switch (LogLevel)
            {
                case LogLevel.Debug:
                    level = "DBG";
                    break;

                case LogLevel.Info:
                    level = "INF";
                    break;

                case LogLevel.Warn:
                    level = "WRN";
                    break;

                case LogLevel.Error:
                    level = "ERR";
                    break;
            }

            return $"{TimeStamp:hh:mm:ss.fff}  [{level}] {Message}";
        }
    }
}