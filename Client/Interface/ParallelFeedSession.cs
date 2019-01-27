#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.Messages;

#endregion

namespace Client.Interface
{
    /// <summary>
    ///     Feed session for multiple nodes
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ParallelFeedSession<T> : IFeedSession where T : class
    {
        private readonly Queue<Task> _tasks;

        public ParallelFeedSession(int nodeCount, int packetSize, bool excludeFromEviction)
        {
            ExcludeFromEviction = excludeFromEviction;
            PacketSize = packetSize;
            NodeCount = nodeCount;

            Requests = new PutRequest[nodeCount];
            for (var i = 0; i < Requests.Length; i++) Requests[i] = new PutRequest(typeof(T));

            _tasks = new Queue<Task>();
        }

        public int NodeCount { get; }
        public int PacketSize { get; }
        public bool ExcludeFromEviction { get; }

        internal bool IsClosed { get; set; }

        public PutRequest[] Requests { get; }

        public void AddTask(Task task)
        {
            // prevent too many pending tasks from accumulating

            lock (_tasks)
            {
                if (_tasks.Count > NodeCount * 2)
                {
                    Task t;
                    do
                    {
                        t = _tasks.Dequeue();
                    } while (t.IsCanceled);

                    t.Wait();
                }

                _tasks.Enqueue(task);
            }
        }

        public void WaitForAll()
        {
            try
            {
                lock (_tasks)
                {
                    Task.WaitAll(_tasks.ToArray());
                }
            }
            catch (AggregateException e)
            {
                throw e.InnerException;
            }
        }
    }
}