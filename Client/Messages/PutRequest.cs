using System;
using System.Collections.Generic;
using Client.Core;
using Client.Queries;
using ProtoBuf;

namespace Client.Messages
{
    /// <summary>
    ///     Put (create or update) an object in the cache
    /// </summary>
    [ProtoContract]
    public class PutRequest : DataRequest
    {
        /// <summary>
        ///     If true, this is the last packet of a feed session
        /// </summary>
        [ProtoMember(4)] private bool _endOfSession;


        /// <summary>
        ///     If true this item is never automatically evicted
        /// </summary>
        [ProtoMember(2)] private bool _excludeFromEviction;

        [ProtoMember(1)] private List<CachedObject> _items = new List<CachedObject>();

        /// <summary>
        ///     If set the put is part of feed session
        /// </summary>
        [ProtoMember(3)] private string _sessionId;

        /// <summary>
        ///     For serialization only
        /// </summary>
        public PutRequest() : base(DataAccessType.Write, string.Empty)
        {
        }

        /// <summary>
        ///     Create a put request for a specified .NET <see cref="Type" />
        /// </summary>
        /// <param name="type"></param>
        public PutRequest(Type type) : base(DataAccessType.Write, type.FullName)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
        }

        public PutRequest(string fullTypeName) : base(DataAccessType.Write, fullTypeName)
        {
            if (string.IsNullOrWhiteSpace(fullTypeName)) throw new ArgumentNullException(nameof(fullTypeName));
        }


        /// <summary>
        ///     If true this item is never automatically evicted
        /// </summary>
        public bool ExcludeFromEviction
        {
            get => _excludeFromEviction;
            set => _excludeFromEviction = value;
        }

        public IList<CachedObject> Items
        {
            get => _items;
            set => _items = new List<CachedObject>(value);
        }

        public string SessionId
        {
            get => _sessionId;
            set => _sessionId = value;
        }

        public bool EndOfSession
        {
            get => _endOfSession;
            set => _endOfSession = value;
        }

        [field: ProtoMember(5)] public bool OnlyIfNew { get; set; }

        [ProtoMember(6)] public OrQuery Predicate { get; set; }


        /// <summary>
        /// Split the request in order to limit data that is sent at once to a stream
        /// </summary>
        /// <returns></returns>
        public IList<PutRequest> SplitWithMaxSize()
        {
            List<PutRequest> result = new List<PutRequest>();

            var request = new PutRequest(FullTypeName)
            {
                EndOfSession = EndOfSession, ExcludeFromEviction = ExcludeFromEviction, SessionId = SessionId
            };

            result.Add(request);
            int size = 0;
            foreach (var item in Items)
            {
                request.Items.Add(item);
                size += item.ObjectData.Length;
                if (size >= 1_000_000_000)
                {
                    request = new PutRequest(FullTypeName)
                    {
                        EndOfSession = EndOfSession, ExcludeFromEviction = ExcludeFromEviction, SessionId = SessionId
                    };

                    result.Add(request);
                    size = 0;
                }
            }


            return result;
        }
    }
}