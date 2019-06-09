using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Server;

namespace Host.HostServices.Logger
{
    /// <summary>
    ///     A very fast async logger
    /// </summary>
    internal class FastLogger : ILog
    {
        private static readonly int MaxMessagesInQueue = 1000;

        private static readonly int MaxMessagesInCache = 1000;

        private static readonly int DelayBetweenWritesInMilliseconds = 1000;

        private static readonly int MaxFilesToKeep = 2;

        private static readonly string LogDirectory = "logs";

        private readonly Queue<string> _logCache = new Queue<string>(MaxMessagesInCache);

        private readonly Queue<Item> _messageQueue = new Queue<Item>(MaxMessagesInQueue);

        private readonly ManualResetEvent _wakeConsumerEvent = new ManualResetEvent(false);

        private volatile bool _shouldStop;
        private Thread _worker;
        private StreamWriter _writer;

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
                if (_messageQueue.Count < MaxMessagesInQueue) _messageQueue.Enqueue(new Item(level, message));
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
            var logFiles = Directory.EnumerateFiles(LogDirectory, "*.log").ToList().OrderBy(n => n).ToList();
            if (logFiles.Count > MaxFilesToKeep)
                for (var i = 0; i < logFiles.Count - MaxFilesToKeep; i++)
                    File.Delete(logFiles[i]);
        }

        public void Start()
        {
            if (!Directory.Exists(LogDirectory)) Directory.CreateDirectory(LogDirectory);


            _worker = new Thread(() =>
            {
                while (!_shouldStop)
                {
                    // wake up every second
                    _wakeConsumerEvent.WaitOne(DelayBetweenWritesInMilliseconds);

                    DoHouseKeeping();

                    var fileName = DateTime.Now.ToString("yyyy-MM-dd") + ".log";

                    _writer = new StreamWriter(Path.Combine(LogDirectory, fileName), true);

                    var newItems = new List<Item>();

                    lock (_messageQueue)
                    {
                        while (_messageQueue.Count > 0)
                        {
                            var msg = _messageQueue.Dequeue();

                            newItems.Add(msg);
                        }
                    }

                    foreach (var item in newItems)
                    {
                        _writer.WriteLine(item);

                        StoreInCache(item.ToString());

                        Console.WriteLine(item);
                    }

                    _writer.Dispose();
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
            return new List<string>(_logCache);
        }

        private enum LogLevel
        {
            Debug,
            Info,
            Warn,
            Error
        }

        private class Item
        {
            public Item(LogLevel logLevel, string message)
            {
                LogLevel = logLevel;
                Message = message;

                TimeStamp = DateTime.Now;
            }

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
}