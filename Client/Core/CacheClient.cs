#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Client.ChannelInterface;
using Client.Interface;
using Client.Messages;
using Client.Queries;
using Client.Tools;

#endregion


namespace Client.Core
{
    /// <summary>
    ///     Client interface to the cache system
    /// </summary>
    internal class CacheClient : ICacheClient
    {
        public const int DefaultPacketSize = 50000;
        public int ShardIndex { get; set; }

        public int ShardsCount { get; set; } = 1;

        /// <summary>
        ///     In order to connect to a server, a client needs a channel for data transport.
        ///     Usually you do not need to explicitly instantiate an <see cref="IClientChannel" /> and connect it to the server.
        ///     Use a factory (like Cachalot.Channel.TCPClientFactory) to instantiate both the client and the channel
        ///     <example>
        ///         CacheClient client = TcpClientFactory.FromElement("CacheClientConfig.xml");
        ///     </example>
        /// </summary>
        public IClientChannel Channel { get; set; }

        public Dictionary<string, ClientSideTypeDescription> KnownTypes { get; } =
            new Dictionary<string, ClientSideTypeDescription>();


        public ClusterInformation GetClusterInformation()
        {
            var response = GetServerDescription();

            return new ClusterInformation(new[] {response});
        }

        public ServerLog GetLog(int lastLines)
        {
            var response = GetServerLog(lastLines);

            return new ServerLog(new[] {response});
        }

        /// <summary>
        /// </summary>
        /// <param name="rw">if true reset normal mode otherwise switch to read-only</param>
        public void SetReadonlyMode(bool rw = false)
        {
            var request = new SwitchModeRequest(rw ? 0 : 1);

            var response = Channel.SendRequest(request);

            if (response is ExceptionResponse exResponse)
                throw new CacheException("Error while resyncing unique id generators", exResponse.Message,
                    exResponse.CallStack);
        }

        public void DropDatabase()
        {
            var request = new DropRequest();

            var response = Channel.SendRequest(request);

            if (response is ExceptionResponse exResponse)
                throw new CacheException("Error while dropping database. ", exResponse.Message,
                    exResponse.CallStack);
        }

        public int[] GenerateUniqueIds(string generatorName, int quantity = 1)
        {
            var request = new GenerateUniqueIdsRequest(quantity, generatorName.ToLower(), ShardIndex, ShardsCount);

            var response = Channel.SendRequest(request);

            if (response is ExceptionResponse exResponse)
                throw new CacheException("Error while generating unique id(s)", exResponse.Message,
                    exResponse.CallStack);

            var r = (GenerateUniqueIdsResponse) response;

            return r.Ids;
        }


        /// <summary>
        ///     Add a new object or update an existent one.The object may be excluded from eviction.
        ///     This feature is useful for the loader components which pre load data into the cache
        /// </summary>
        /// <param name="item"></param>
        /// <param name="excludeFromEviction">if true, the item will never be automatically evicted </param>
        public void Put<T>(T item, bool excludeFromEviction = false)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            var description = RegisterTypeIfNeeded(typeof(T));

            var packedItem = CachedObject.Pack(item, description);

            var request = new PutRequest(typeof(T)) {ExcludeFromEviction = excludeFromEviction};

            request.Items.Add(packedItem);

            var response = Channel.SendRequest(request);

            if (response is ExceptionResponse exResponse)
                throw new CacheException("Error while writing an object to the cache", exResponse.Message,
                    exResponse.CallStack);
        }


        /// <summary>
        ///     Dump all data to an external file
        /// </summary>
        /// <param name="path"></param>
        public void Dump(string path)
        {
            var request = new DumpRequest {Path = path, ShardIndex = ShardIndex};
            var response = Channel.SendRequest(request);

            if (response is ExceptionResponse exResponse)
                throw new CacheException("Error while dumping all data", exResponse.Message,
                    exResponse.CallStack);
        }

        /// <summary>
        ///     Restore all data from dump
        /// </summary>
        /// <param name="path"></param>
        public void ImportDump(string path)
        {
            try
            {
                ImportDumpStage0(path);
                ImportDumpStage1(path);
                ImportDumpStage2(path);
            }
            catch (CacheException)
            {
                ImportDumpStage3(path);

                throw;
            }
            catch (Exception e)
            {
                // should never reach this code
                throw new CacheException("", e.Message, "");
            }
        }

        public void InitializeFromDump(string path)
        {
            path = DumpHelper.NormalizeDumpPath(path);

            var schemaPath = Path.Combine(path, "schema.json");

            var json = File.ReadAllText(schemaPath);

            var schema = SerializationHelper.DeserializeJson<Schema>(json);

            foreach (var typeDescription in schema.TypeDescriptions)
            {
                // register the type on the server

                var request = new RegisterTypeRequest(typeDescription);

                var response = Channel.SendRequest(request);

                if (response is ExceptionResponse exResponse)
                    throw new CacheException("Error while registering a type on the server", exResponse.Message,
                        exResponse.CallStack);

                FeedMany(DumpHelper.ObjectsInDump(path, typeDescription), true);
            }

            // reinitialize the sequences. As the shard count has probably changed reinitialize all the sequences in each shard
            // starting with the maximum value

            var maxValues = new Dictionary<string, int>();

            var files = Directory.EnumerateFiles(path, "sequence_*.json");

            foreach (var file in files)
            {
                var sequenceJson = File.ReadAllText(file);
                var seq = SerializationHelper.DeserializeJson<Dictionary<string, int>>(sequenceJson);
                foreach (var pair in seq)
                {
                    var keyFound = maxValues.ContainsKey(pair.Key);
                    if (keyFound && maxValues[pair.Key] < pair.Value || !keyFound) maxValues[pair.Key] = pair.Value;
                }
            }

            // resync sequences on the server

            ResyncUniqueIds(maxValues);
        }

        /// <summary>
        ///     Transactionally add or replace a collection of objects of the same type
        ///     This method needs to lock the cache during the update. Do not use for large collections
        ///     (for example while pre-loading data to a cache) Use <see cref="FeedMany{T}(IEnumerable{T},bool)" /> instead
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="items"></param>
        /// <param name="excludeFromEviction"></param>
        public void PutMany<T>(IEnumerable<T> items, bool excludeFromEviction)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            var description = RegisterTypeIfNeeded(typeof(T));

            var request = new PutRequest(typeof(T));

            foreach (var item in items)
            {
                var packedItem = CachedObject.Pack(item, description);
                request.Items.Add(packedItem);
            }


            request.ExcludeFromEviction = excludeFromEviction;

            var response = Channel.SendRequest(request);

            if (response is ExceptionResponse exResponse)
                throw new CacheException("Error while writing an object ", exResponse.Message,
                    exResponse.CallStack);
        }

        /// <summary>
        ///     Add or replace a collection of cached objects. This method is not transactional. The
        ///     cache is fed by packet of <see cref="DefaultPacketSize" /> objects.
        ///     As we do not require a transactional update for the whole collection, the lock time is much smaller
        ///     Use this method when pre loading data to the cache. Use <see cref="PutMany{T}" /> when you need
        ///     transactional update for a small collection of objects
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="items"></param>
        /// <param name="excludeFromEviction"></param>
        public void FeedMany<T>(IEnumerable<T> items, bool excludeFromEviction)
        {
            FeedMany(items, excludeFromEviction, DefaultPacketSize);
        }


        public void FeedMany<T>(IEnumerable<T> items, bool excludeFromEviction, int packetSize)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            var itemType = typeof(T);

            ClientSideTypeDescription description = null;
            var needPack = itemType != typeof(CachedObject);
            string typeName = null;

            var sessionId = Guid.NewGuid().ToString();

            if (needPack)
            {
                description = RegisterTypeIfNeeded(itemType);
                typeName = description.FullTypeName;
            }


            using (var enumerator = items.GetEnumerator())
            {
                var endLoop = false;

                while (!endLoop)
                {
                    var packet = new CachedObject[packetSize];
                    var toPack = new T[packetSize];

                    var count = 0;
                    for (var i = 0; i < packetSize; i++)
                        if (enumerator.MoveNext())
                        {
                            var item = enumerator.Current;
                            toPack[i] = item;
                            count++;
                        }
                        else
                        {
                            endLoop = true;
                            break;
                        }


                    Parallel.For(0, count, new ParallelOptions {MaxDegreeOfParallelism = 10}, (i, loopState) =>
                        {
                            var item = toPack[i];
                            var packedItem = needPack ? CachedObject.Pack(item, description) : item as CachedObject;

                            if (typeName == null) typeName = packedItem?.FullTypeName;

                            packet[i] = packedItem;
                        }
                    );


                    if (typeName != null) // null only for empty collection
                    {
                        var request = new PutRequest(typeName)
                        {
                            ExcludeFromEviction = excludeFromEviction,
                            SessionId = sessionId,
                            EndOfSession = endLoop
                        };

                        foreach (var cachedObject in packet)
                            if (cachedObject != null)
                                request.Items.Add(cachedObject);

                        
                        var split = request.SplitWithMaxSize();

                        foreach (var putRequest in split)
                        {
                            var response =
                                Channel.SendRequest(putRequest);
                            if (response is ExceptionResponse exResponse)
                                throw new CacheException(
                                    "Error while writing an object to the cache",
                                    exResponse.Message, exResponse.CallStack);    
                        }
                        
                    }
                }
            }
        }

        /// <summary>
        ///     As an alternative to <see cref="FeedMany{T}(IEnumerable{T},bool,int)" /> you can use
        ///     <see cref="BeginFeed{TItem}" /> <see cref="Add{TItem}" /> <see cref="EndFeed{TItem}" />
        /// </summary>
        /// <typeparam name="TItem"></typeparam>
        /// <returns></returns>
        public IFeedSession BeginFeed<TItem>(int packetSize, bool excludeFromEviction) where TItem : class
        {
            RegisterTypeIfNeeded(typeof(TItem));


            return new FeedSession<TItem>(packetSize, excludeFromEviction);
        }
        //public IFeedSession BeginFeed(TypeDescriptionConfig typeDescription, int packetSize, bool excludeFromEviction) 
        //{
        //    //RegisterTypeIfNeeded()


        //    //return new FeedSession<TItem>(packetSize, excludeFromEviction);

        //    throw new NotImplementedException();
        //}

        /// <summary>
        ///     As an alternative to <see cref="FeedMany{T}(IEnumerable{T},bool,int)" /> you can use
        ///     <see cref="BeginFeed{TItem}" /> <see cref="Add{TItem}" /> <see cref="EndFeed{TItem}" />
        /// </summary>
        /// <typeparam name="TItem"></typeparam>
        /// <param name="session"></param>
        /// <param name="item"></param>
        public void Add<TItem>(IFeedSession session, TItem item) where TItem : class
        {
            var sessionImplementation = (FeedSession<TItem>) session;

            if (sessionImplementation.IsClosed)
                throw new CacheException("The feed session is closed");

            var description = RegisterTypeIfNeeded(typeof(TItem));

            var packedItem = CachedObject.Pack(item, description);

            sessionImplementation.Request.Items.Add(packedItem);

            if (sessionImplementation.Request.Items.Count == sessionImplementation.PacketSize)
            {
                var request = sessionImplementation.Request;
                sessionImplementation.Request = null;

                // only one packet at a time is fed asynchronously. If the previous send is still pending wait fot it to finish
                sessionImplementation.WaitForAsyncCompletion();


                ThreadPool.QueueUserWorkItem(state =>
                {
                    try
                    {
                        var rq = (Request) state;

                        sessionImplementation.StartAsync();

                        var response = Channel.SendRequest(rq);
                        if (response is ExceptionResponse exResponse)
                            sessionImplementation.EndAsync(
                                new CacheException(
                                    "Error while writing an object to the cache",
                                    exResponse.Message, exResponse.CallStack));
                        else
                            sessionImplementation.EndAsync(null);
                    }
                    catch (Exception e)
                    {
                        sessionImplementation.EndAsync(e);
                    }
                }, request);


                // prepare a new empty put request that will receive new items
                sessionImplementation.Request = new PutRequest(typeof(TItem));
            }
        }


        /// <summary>
        ///     As an alternative to <see cref="FeedMany{T}(IEnumerable{T},bool,int)" /> you can use
        ///     <see cref="BeginFeed{TItem}" /> <see cref="Add{TItem}" /> <see cref="EndFeed{TItem}" />
        /// </summary>
        /// <typeparam name="TItem"></typeparam>
        /// <param name="session"></param>
        public void EndFeed<TItem>(IFeedSession session) where TItem : class
        {
            var feedSession = (FeedSession<TItem>) session;
            feedSession.WaitForAsyncCompletion();

            // the last block is always send synchronously.
            if (feedSession.Request.Items.Count > 0)
            {
                var response = Channel.SendRequest(feedSession.Request);
                if (response is ExceptionResponse exResponse)
                    throw new CacheException("Error while writing an object to the cache", exResponse.Message,
                        exResponse.CallStack);
            }

            feedSession.IsClosed = true;
        }


        /// <summary>
        ///     remove all the items matching a query
        /// </summary>
        /// <param name="query"></param>
        /// <returns>number of items effectively removed</returns>
        public int RemoveMany(OrQuery query)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            var request = new RemoveManyRequest(query);

            var response = Channel.SendRequest(request);

            if (response is ExceptionResponse exResponse)
                throw new CacheException("Error in RemoveMany", exResponse.Message, exResponse.CallStack);

            if (!(response is ItemsCountResponse countResponse))
                throw new CacheException("Invalid type of response received in RemoveMany()");

            return countResponse.ItemsCount;
        }


        /// <summary>
        ///     Truncate clears the cache for a given data type and also resets the hit ratio
        ///     It is much faster than <see cref="RemoveMany" /> with a query that matches all data
        /// </summary>
        /// <typeparam name="TItem"></typeparam>
        /// <returns></returns>
        public int Truncate<TItem>()
        {
            // as we pass an empty query, it will be treated as a special request by the server
            return RemoveMany(new OrQuery(typeof(TItem)));
        }


        /// <summary>
        ///     Get one element by primary key
        /// </summary>
        /// <param name="value">value of the primary key to look for</param>
        /// <returns>the found object or null if none available</returns>
        public TItemType GetOne<TItemType>(object value)
        {
            var description = RegisterTypeIfNeeded(typeof(TItemType)).AsTypeDescription;

            var builder = new QueryBuilder(description);

            var query = builder.GetOne(value);

            var result = InternalGetMany<TItemType>(query);

            return (TItemType) result.FirstOrDefault()?.Item;
        }

        /// <summary>
        ///     Get one element by unique key
        /// </summary>
        /// <param name="keyName">name of the unique key</param>
        /// <param name="value">value to look for</param>
        /// <returns>the found object or null if none available</returns>
        public TItemType GetOne<TItemType>(string keyName, object value)
        {
            var builder = new QueryBuilder(typeof(TItemType));

            var query = builder.GetOne(keyName, value);

            var result = InternalGetMany<TItemType>(query);

            return (TItemType) result.FirstOrDefault()?.Item;
        }


        /// <summary>
        ///     Remove an item from the cache.
        /// </summary>
        /// <typeparam name="TItemType"></typeparam>
        /// <param name="primaryKeyValue"></param>
        public void Remove<TItemType>(object primaryKeyValue)
        {
            InternalRemove<TItemType>(primaryKeyValue);
        }

        public void DeclareDataFullyLoaded<TItemType>(bool isFullyLoaded)
        {
            DeclareDomain(new DomainDescription(OrQuery.Empty<TItemType>(), isFullyLoaded));
        }


        public bool IsDataFullyLoaded<TItemType>()
        {
            var serverDescription = GetServerDescription();

            // ReSharper disable AssignNullToNotNullAttribute
            if (serverDescription.DataStoreInfoByFullName.TryGetValue(typeof(TItemType).FullName, out var info))
                // ReSharper restore AssignNullToNotNullAttribute
                return info.AvailableData.IsFullyLoaded;

            return false;
        }

        public bool Ping()
        {
            try
            {
                var description = GetServerDescription();
                return description.ServerProcessInfo.ConnectedClients > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        ///     Explicitly registers a type on the client and the server. In order to use this version of the method
        ///     the type need to be tagged(attributes need to be associated to its public properties used as keys)
        ///     Explicit type registration is not required.
        ///     It is done automatically when the first Put() is executed for an item of the specified type
        ///     or when a query is first built
        ///     <br />
        ///     As type registration is an expensive operation, you may want to do it during the client initialization.
        /// </summary>
        /// <param name="type"></param>
        public ClientSideTypeDescription RegisterTypeIfNeeded(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            if (KnownTypes.TryGetValue(type.FullName ?? throw new InvalidOperationException(), out var typeDescription))
                return typeDescription;

            typeDescription = ClientSideTypeDescription.RegisterType(type);
            KnownTypes[typeDescription.FullTypeName] = typeDescription;


            var request = new RegisterTypeRequest(typeDescription.AsTypeDescription, ShardIndex, ShardsCount);

            var response = Channel.SendRequest(request);

            if (response is ExceptionResponse exResponse)
                throw new CacheException("Error while registering a type on the server", exResponse.Message,
                    exResponse.CallStack);

            return typeDescription;
        }

        /// <summary>
        ///     Register a type as cacheable with an external description
        ///     Cacheable type descriptions can be provided by attributes on the public properties
        ///     or as <see cref="TypeDescriptionConfig" />.
        ///     If an external description is required, the type must be explicitly registered.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="description"></param>
        public ClientSideTypeDescription RegisterTypeIfNeeded(Type type, TypeDescriptionConfig description)
        {
            if (KnownTypes.TryGetValue(description.FullTypeName, out var typeDescription)) return typeDescription;

            typeDescription = ClientSideTypeDescription.RegisterType(type, description);

            KnownTypes[typeDescription.FullTypeName] = typeDescription;


            var request = new RegisterTypeRequest(typeDescription.AsTypeDescription, ShardIndex, ShardsCount);

            var response = Channel.SendRequest(request);

            if (response is ExceptionResponse exResponse)
                throw new CacheException("Error while registering a type on the server", exResponse.Message,
                    exResponse.CallStack);

            return typeDescription;
        }


        /// <summary>
        ///     Count the items matching the query and check for data completeness.
        /// </summary>
        /// <param name="query"></param>
        /// <returns>completeness, items count</returns>
        public KeyValuePair<bool, int> EvalQuery(OrQuery query)
        {
            var request = new EvalRequest(query);

            var response = Channel.SendRequest(request);

            if (response is ExceptionResponse exResponse)
                throw new CacheException("Error while getting server information", exResponse.Message,
                    exResponse.CallStack);

            var concreteResponse = (EvalResponse) response;
            return new KeyValuePair<bool, int>(concreteResponse.Complete, concreteResponse.Items);
        }


        /// <summary>
        ///     Return cached object without deserialization to .NET object. Used to process data at json level
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public IList<CachedObject> GetObjectDescriptions(OrQuery query)
        {
            var exceptions = new List<ExceptionResponse>();
            var result = new List<CachedObject>();
            var request = new GetDescriptionRequest(query);
            Channel.SendStreamRequest(request, (CachedObject item, int currentItem, int totalItems) => result.Add(item),
                exceptions.Add);


            if (exceptions.Count > 0)
                throw new CacheException("Error in GetObjectDescriptions", exceptions[0].Message,
                    exceptions[0].CallStack);

            return result;
        }

        public void Stop(bool restart)
        {
            var request = new StopRequest(restart);

            Channel.SendRequest(request);
        }

        public void ExecuteTransaction(IList<CachedObject> itemsToPut, IList<OrQuery> conditions,
            IList<CachedObject> itemsToDelete = null)
        {
            var locksOk = false;

            var iteration = 0;


            while (!locksOk)
            {
                var delay = ThreadLocalRandom.Instance.Next(10 * iteration);

                TransactionStatistics.Retries(iteration + 1);


                Dbg.Trace(
                    $"C: delay = {delay} for iteration {iteration} single stage transaction connector {GetHashCode()}");

                if (delay > 0)
                    Thread.Sleep(delay);


                if (itemsToPut == null) throw new ArgumentNullException(nameof(itemsToPut));

                var request = new TransactionRequest(itemsToPut, conditions, itemsToDelete)
                    {IsSingleStage = true};

                TransactionStatistics.ExecutedAsSingleStage();


                var response = Channel.SendRequest(request);

                if (response is NullResponse)
                    locksOk = true;
                else if (response is ExceptionResponse exResponse)
                    if (exResponse.ExceptionType != ExceptionType.FailedToAcquireLock)
                        throw new CacheException(exResponse.Message, exResponse.ExceptionType);
            }

            TransactionStatistics.NewTransactionCompleted();
        }

        public void Import(string jsonFile)
        {
            var objects = DumpHelper.LoadObjects(jsonFile, this);

            FeedMany(objects, true);
        }

        public bool TryAdd<T>(T item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            CachedObject packedItem;
            if (typeof(T) == typeof(CachedObject))
            {
                packedItem = item as CachedObject;
            }
            else
            {
                var description = RegisterTypeIfNeeded(typeof(T));

                packedItem = CachedObject.Pack(item, description);
            }


            var request = new PutRequest(typeof(T)) {ExcludeFromEviction = true, OnlyIfNew = true};

            request.Items.Add(packedItem);

            var response = Channel.SendRequest(request);

            if (response is ExceptionResponse exResponse)
                throw new CacheException("Error while writing an object to the cache", exResponse.Message,
                    exResponse.CallStack);

            if (response is ItemsCountResponse count) return count.ItemsCount > 0;

            throw new NotSupportedException($"Unknown answer type received in TryAdd:{response.GetType()}");
        }

        public void UpdateIf<T>(T newValue, OrQuery testAsQuery)
        {
            if (newValue == null)
                throw new ArgumentNullException(nameof(newValue));

            CachedObject packedItem;
            if (typeof(T) == typeof(CachedObject))
            {
                packedItem = newValue as CachedObject;
            }
            else
            {
                var description = RegisterTypeIfNeeded(typeof(T));

                packedItem = CachedObject.Pack(newValue, description);
            }


            var request = new PutRequest(typeof(T)) {ExcludeFromEviction = true, Predicate = testAsQuery};

            request.Items.Add(packedItem);

            var response = Channel.SendRequest(request);

            if (response is ExceptionResponse exResponse)
                throw new CacheException("Error while writing an object to the cache", exResponse.Message,
                    exResponse.CallStack);
        }


        /// <summary>
        ///     Declare a subset of data as being fully available in the cache.<br />
        ///     Used by loader components to declare data preloaded in the cache.
        ///     <seealso cref="DomainDescription" />
        /// </summary>
        /// <param name="domain">data description</param>
        public void DeclareDomain(DomainDescription domain)
        {
            if (domain == null)
                throw new ArgumentNullException(nameof(domain));

            if (domain.DescriptionAsQuery.TypeName == null)
                throw new ArgumentNullException(nameof(domain), "TypeName not specified");

            var request = new DomainDeclarationRequest(domain);
            var response = Channel.SendRequest(request);

            if (response is ExceptionResponse exResponse)
                throw new CacheException("Error while declaring a domain", exResponse.Message, exResponse.CallStack);
        }

        /// <summary>
        /// </summary>
        /// <param name="fullTypeName"></param>
        /// <param name="evictionType"></param>
        /// <param name="limit"></param>
        /// <param name="itemsToRemove"></param>
        /// <param name="timeLimitInMilliseconds"></param>
        public void ConfigEviction(string fullTypeName, EvictionType evictionType, int limit, int itemsToRemove, int timeLimitInMilliseconds = 0)
        {
            if (evictionType == EvictionType.LessRecentlyUsed && timeLimitInMilliseconds != 0)
            {
                throw new ArgumentException($"{nameof(timeLimitInMilliseconds)} can be used only for TTL eviction");
            }

            if (evictionType == EvictionType.TimeToLive && (limit != 0 || itemsToRemove != 0))
            {
                throw new ArgumentException($"{nameof(limit)} and {nameof(itemsToRemove)} can be used only for LRU eviction");
            }

            var request = new EvictionSetupRequest(fullTypeName, evictionType, limit, itemsToRemove, timeLimitInMilliseconds);

            var response = Channel.SendRequest(request);

            if (response is ExceptionResponse exResponse)
                throw new CacheException("Error while declaring a domain", exResponse.Message, exResponse.CallStack);
        }

        /// <summary>
        ///     Retrieve multiple objects using a precompiled query.<br />
        ///     To create a valid <see cref="OrQuery" /> use a <see cref="QueryBuilder" />.
        /// </summary>
        /// <typeparam name="TItemType"></typeparam>
        /// <param name="query"></param>
        /// <returns></returns>
        public IEnumerable<TItemType> GetMany<TItemType>(OrQuery query)
        {
            return InternalGetMany<TItemType>(query).Select(ri=>ri.Item).Cast<TItemType>();
        }

        public IEnumerable<RankedItem> GetManyWithRank<TItemType>(OrQuery query)
        {
            return InternalGetMany<TItemType>(query);
        }

        public void ResyncUniqueIds(IDictionary<string, int> newValues)
        {
            var request = new ResyncUniqueIdsRequest(new Dictionary<string, int>(newValues), ShardIndex, ShardsCount);

            var response = Channel.SendRequest(request);

            if (response is ExceptionResponse exResponse)
                throw new CacheException("Error while resyncing unique id generators", exResponse.Message,
                    exResponse.CallStack);
        }


        /// <summary>
        ///     First stage of dump files import. The server is switched to admin mode and data files are moved to allow rollback
        /// </summary>
        /// <param name="path"></param>
        public void ImportDumpStage0(string path)
        {
            var request = new ImportDumpRequest {Path = path, ShardIndex = ShardIndex, Stage = 0};

            var response = Channel.SendRequest(request);

            if (response is ExceptionResponse exceptionResponse)
                throw new CacheException("Error while importing dump", exceptionResponse.Message,
                    exceptionResponse.CallStack);
        }

        /// <summary>
        ///     Import data from dump files
        /// </summary>
        /// <param name="path"></param>
        public void ImportDumpStage1(string path)
        {
            var request = new ImportDumpRequest {Path = path, Stage = 1, ShardIndex = ShardIndex};

            var response = Channel.SendRequest(request);

            if (response is ExceptionResponse exceptionResponse)
                throw new CacheException("Error while importing dump.", exceptionResponse.Message,
                    exceptionResponse.CallStack);
        }

        /// <summary>
        ///     Last stage of successful dump import. Delete backup files and disable the admin mode
        /// </summary>
        /// <param name="path"></param>
        public void ImportDumpStage2(string path)
        {
            var request = new ImportDumpRequest {Path = path, Stage = 2, ShardIndex = ShardIndex};

            var response = Channel.SendRequest(request);

            if (response is ExceptionResponse exceptionResponse)
                throw new CacheException(
                    "Error while switching off admin mode. Dump import was successful but you need to manually restart the servers",
                    exceptionResponse.Message,
                    exceptionResponse.CallStack);
        }

        /// <summary>
        ///     Rollback in case of unsuccessful dump import
        /// </summary>
        /// <param name="path"></param>
        public void ImportDumpStage3(string path)
        {
            var request = new ImportDumpRequest {Path = path, Stage = 3, ShardIndex = ShardIndex};

            var response = Channel.SendRequest(request);

            if (response is ExceptionResponse exceptionResponse)
                throw new CacheException("Error during rollback", exceptionResponse.Message,
                    exceptionResponse.CallStack);
        }

        /// <summary>
        ///     Get information about the last <see cref="lines" /> requests processed by the server
        ///     Useful information include request type, number of items processed, server processing time
        /// </summary>
        /// <param name="lines">number of log entries to retrieve</param>
        /// <returns></returns>
        public LogResponse GetServerLog(int lines)
        {
            var request = new LogRequest(lines);

            var response = Channel.SendRequest(request);

            if (response is ExceptionResponse exResponse)
                throw new CacheException("Error while retrieving server log", exResponse.Message, exResponse.CallStack);

            return response as LogResponse;
        }


        /// <summary>
        ///     Get all known type descriptions from the server. Used by generic clients (like cache monitoring tools)
        ///     to produce queries without having dependencies to the .NET type.
        /// </summary>
        /// <returns></returns>
        public IDictionary<string, TypeDescription> GetKnownTypes()
        {
            var request = new GetKnownTypesRequest();

            var response = Channel.SendRequest(request);

            if (response is ExceptionResponse exResponse)
                throw new CacheException("Error while getting server information", exResponse.Message,
                    exResponse.CallStack);

            var concreteResponse = (ServerDescriptionResponse) response;

            return concreteResponse.KnownTypesByFullName;
        }


        /// <summary>
        ///     Return useful information about the server process and the "data stores"
        ///     A "data store" is a data container on the server containing all the objects of a given type
        /// </summary>
        /// <returns></returns>
        public ServerDescriptionResponse GetServerDescription()
        {
            var request = new GetKnownTypesRequest();

            var response = Channel.SendRequest(request);

            if (response is ExceptionResponse exResponse)
                throw new CacheException("Error while getting server information", exResponse.Message,
                    exResponse.CallStack);

            var concreteResponse = response as ServerDescriptionResponse;
            Dbg.CheckThat(concreteResponse != null);
            return concreteResponse;
        }

        private IEnumerable<RankedItem> InternalGetMany<TItemType>(OrQuery query)
        {
            var request = new GetRequest(query);

            if(query.IsFullTextQuery)
                return Channel.SendStreamRequest<TItemType>(request);

            return Channel.SendStreamRequest<TItemType>(request);
        }


        /// <summary>
        ///     Retrieve multiple objects using a WHERE-like query string
        ///     a=1, b = XXX is equivalent to WHERE a=1 AND b=XXX
        /// </summary>
        /// <typeparam name="TItemType"></typeparam>
        /// <param name="sqlLike"></param>
        /// <returns></returns>
        public IEnumerable<TItemType> GetManyWhere<TItemType>(string sqlLike)

        {
            var builder = new QueryBuilder(typeof(TItemType));

            var query = builder.GetManyWhere(sqlLike);

            return InternalGetMany<TItemType>(query).Select(ri=> ri.Item).Cast<TItemType>();
        }

        /// <summary>
        ///     Remove an item from the cache. If cacheOnly == false the item is also removed
        ///     from the underlying persistent storage
        /// </summary>
        /// <typeparam name="TItemType"></typeparam>
        /// <param name="primaryKeyValue"></param>
        private void InternalRemove<TItemType>(object primaryKeyValue)
        {
            var description = RegisterTypeIfNeeded(typeof(TItemType)).AsTypeDescription;

            KeyValue primaryKey;

            if (primaryKeyValue is KeyValue kv)
                primaryKey = kv;
            else
                primaryKey = description.MakePrimaryKeyValue(primaryKeyValue);

            var request = new RemoveRequest(typeof(TItemType), primaryKey);


            var response = Channel.SendRequest(request);

            if (response is ExceptionResponse exResponse)
                throw new CacheException("Error while removing an object from the cache", exResponse.Message,
                    exResponse.CallStack);
        }


        #region IDisposable Members

        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                Channel?.Dispose();

                _disposed = true;
            }
        }

        #endregion
    }
}