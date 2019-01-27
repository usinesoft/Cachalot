using System;
using System.Threading;
using Client.Interface;
using Client.Messages;

namespace Client.Core
{
    /// <summary>
    ///     Feed with json
    /// </summary>
    public class FeedSession : IFeedSession
    {
        private readonly ManualResetEvent _workingEvent = new ManualResetEvent(true);

        private Exception _lastProcessingException;

        internal FeedSession(TypeDescription typeDescription, int packetSize, bool excludeFromEviction)
        {
            Request = new PutRequest(typeDescription.FullTypeName) {ExcludeFromEviction = excludeFromEviction};
            PacketSize = packetSize;
        }

        private PutRequest Request { get; }

        private int PacketSize { get; }

        internal bool IsClosed { get; set; }

        public void StartAsync()
        {
            _workingEvent.Reset();
            _lastProcessingException = null;
        }


        public void EndAsync(Exception exception)
        {
            _lastProcessingException = exception;
            _workingEvent.Set();
        }

        public void WaitForAsyncCompletion()
        {
            _workingEvent.WaitOne();
            if (_lastProcessingException != null)
                throw _lastProcessingException;
        }
    }

    /// <summary>
    ///     A feed session allows the cache to be fed with large collections of objects
    /// </summary>
    public class FeedSession<TItem> : IFeedSession where TItem : class
    {
        private readonly ManualResetEvent _workingEvent = new ManualResetEvent(true);

        private Exception _lastProcessingException;

        internal FeedSession(int packetSize, bool excludeFromEviction)
        {
            Request = new PutRequest(typeof(TItem)) {ExcludeFromEviction = excludeFromEviction};
            PacketSize = packetSize;
        }

        internal PutRequest Request { get; set; }

        internal int PacketSize { get; }

        internal bool IsClosed { get; set; }

        public void StartAsync()
        {
            _workingEvent.Reset();
            _lastProcessingException = null;
        }


        public void EndAsync(Exception exception)
        {
            _lastProcessingException = exception;
            _workingEvent.Set();
        }

        public void WaitForAsyncCompletion()
        {
            _workingEvent.WaitOne();
            if (_lastProcessingException != null)
                throw _lastProcessingException;
        }
    }
}