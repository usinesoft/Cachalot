using System;
using ProtoBuf;

namespace Client.Messages
{
    /// <summary>
    ///     An entry in the server log
    /// </summary>
    [ProtoContract]
    [Serializable]
    public class ServerLogEntry
    {
        /// <summary>
        ///     Potential lock time of the cache (as a lock free scheduler is used there is no effective lock
        ///     for read-only operations; this is the lock time in the worst case scenario)
        /// </summary>
        [ProtoMember(1)] private double _cacheAccessTimeInMilliseconds;

        /// <summary>
        ///     Number of items processed by a request
        /// </summary>
        [ProtoMember(4)] private int _itemsProcessed;


        /// <summary>
        ///     Free form message
        /// </summary>
        [ProtoMember(3)] private string _message;

        /// <summary>
        ///     Type of request(GET, PUT, COUNT...) encoded as a string for extensibility
        /// </summary>
        [ProtoMember(2)] private string _requestType;


        public ServerLogEntry(double cacheAccessTimeInMilliseconds, string requestType, string message,
            int itemsProcessed)
        {
            _cacheAccessTimeInMilliseconds = cacheAccessTimeInMilliseconds;
            _requestType = requestType;
            _message = message;
            _itemsProcessed = itemsProcessed;
            TimeStamp = DateTime.Now;
        }


        public ServerLogEntry()
        {
            TimeStamp = DateTime.Now;
        }

        /// <summary>
        ///     Potential lock time of the cache (as a lock free scheduler is used there is no effective lock
        ///     for read-only operations; this is the lock time in the worst case scenario)
        /// </summary>
        public double CacheAccessTimeInMilliseconds
        {
            get => _cacheAccessTimeInMilliseconds;
            set => _cacheAccessTimeInMilliseconds = value;
        }

        /// <summary>
        ///     Type of request(GET, PUT, COUNT...) encoded as a string for extensibility
        /// </summary>
        public string RequestType
        {
            get => _requestType;
            set => _requestType = value;
        }

        /// <summary>
        ///     Free form message
        /// </summary>
        public string Message
        {
            get => _message;
            set => _message = value;
        }

        /// <summary>
        ///     Number of items processed by a request
        /// </summary>
        public int ItemsProcessed
        {
            get => _itemsProcessed;
            set => _itemsProcessed = value;
        }

        [field: ProtoMember(5)] public DateTime TimeStamp { get; }


        public override string ToString()
        {
            return
                $"{_requestType}\t{_cacheAccessTimeInMilliseconds:F3} ms\t{_itemsProcessed} items\t{_message}";
        }
    }
}