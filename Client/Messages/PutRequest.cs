using System;
using System.Collections.Generic;
using Client.Core;
using Client.Queries;
using JetBrains.Annotations;
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

        [ProtoMember(1)] private List<PackedObject> _items = new List<PackedObject>();

       
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
        public PutRequest(Type type) : base(DataAccessType.Write, type.Name)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
        }

        public PutRequest(string collectionName) : base(DataAccessType.Write, collectionName)
        {
            if (string.IsNullOrWhiteSpace(collectionName)) throw new ArgumentNullException(nameof(collectionName));
        }


        /// <summary>
        ///     If true this item is never automatically evicted
        /// </summary>
        public bool ExcludeFromEviction
        {
            get => _excludeFromEviction;
            set => _excludeFromEviction = value;
        }

        public IList<PackedObject> Items
        {
            get => _items;
            set => _items = new List<PackedObject>(value);
        }


        public bool EndOfSession
        {
            get => _endOfSession;
            set => _endOfSession = value;
        }

        [field: ProtoMember(5)] public bool OnlyIfNew { get; set; }

        [ProtoMember(6)] public OrQuery Predicate { get; set; }

        public bool HasCondition => Predicate != null && !Predicate.IsEmpty();


        /// <summary>
        /// Split the request in order to limit data that is sent at once to a stream
        /// </summary>
        /// <returns></returns>
        public IList<PutRequest> SplitWithMaxSize()
        {
            List<PutRequest> result = new List<PutRequest>();

            var request = new PutRequest(CollectionName)
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
                    request = new PutRequest(CollectionName)
                    {
                        EndOfSession = EndOfSession, ExcludeFromEviction = ExcludeFromEviction, SessionId = SessionId
                    };

                    result.Add(request);
                    size = 0;
                }
            }


            return result;
        }

        public IDictionary<int, PutRequest> SplitByServer([NotNull] Func<KeyValue, int> serverSelector )
        {
            if (serverSelector == null) throw new ArgumentNullException(nameof(serverSelector));
            
            var result = new Dictionary<int, PutRequest>();

            
            foreach (var item in Items)
            {
                int serverIndex = serverSelector(item.PrimaryKey);

                if (!result.TryGetValue(serverIndex, out var putRequest))
                {
                    putRequest = new PutRequest(CollectionName);
                    result.Add(serverIndex, putRequest);
                }

                putRequest.Items.Add(item);
            }


            return result;
        }
    }
}