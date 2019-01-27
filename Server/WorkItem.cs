using System;
using System.Diagnostics;
using System.Threading;

namespace Server
{
    /// <summary>
    ///     An abstract workitem to be processed. Processing the workitem will require
    ///     access to a shared resource. The workitem can require write or read-only access
    ///     to the shared resource
    /// </summary>
    /// <typeparam name="TInputData"></typeparam>
    public class WorkItem<TInputData> where TInputData : class
    {
        
        /// <summary>
        ///     If true this is a special workitem used as stop marker
        /// </summary>
        private readonly bool _isStopMarker;

        private readonly Action<TInputData> _worker;


        /// <summary>
        ///     default constructor used to create a stop marker workitem
        /// </summary>
        public WorkItem()
        {
            _isStopMarker = true;
        }

        /// <summary>
        ///     Create a new workitem. The workitem needs a worker method. We can optionally specify input data
        /// </summary>
        /// <param name="writeAccess">odes he need write access to shared data</param>
        /// <param name="input">optional input data</param>
        /// <param name="worker">the worker method</param>
        public WorkItem(bool writeAccess, TInputData input, Action<TInputData> worker)
        {
            NeedsWriteAccess = writeAccess;
            Input = input;
            _worker = worker;
            Id = Interlocked.Increment(ref _lastId);
        }

        /// <summary>
        ///     True if write access is needed, false otherwise
        /// </summary>
        public bool NeedsWriteAccess { get; }

        public TInputData Input { get; }

        public static long ExecutedTaskCount => Interlocked.Read(ref _executedTaskCount);

        public long Id { get; }

        /// <summary>
        ///     If true it is a special workitem used as stop marker
        /// </summary>
        public bool IsStopMarker => _isStopMarker;

        /// <summary>
        ///     For debug only
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (_isStopMarker)
                return "*";

            string result = "w";
            if (!NeedsWriteAccess)
                result = "r";
            result += Id;

            return result;
        }

        public static void ResetTaskCount()
        {
            Interlocked.Exchange(ref _executedTaskCount, 0);
        }

        public void Run()
        {
            Interlocked.Increment(ref _executedTaskCount);
            Debug.Assert(_worker != null); //can be null only for a stop marker which is not executed
            _worker(Input);
        }

        //data used mostly for unit testing
// ReSharper disable StaticFieldInGenericType
        private static long _executedTaskCount;

        private static long _lastId;
// ReSharper restore StaticFieldInGenericType
    }
}