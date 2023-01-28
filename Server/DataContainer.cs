//#define DEBUG_VERBOSE

#region

using Client;
using Client.ChannelInterface;
using Client.Core;
using Client.Interface;
using Client.Messages;
using Client.Messages.Pivot;
using Client.Tools;
using Server.Persistence;
using Server.Queries;
using Server.Transactions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Constants = Server.Persistence.Constants;

#endregion

namespace Server
{
    /// <summary>
    ///     The data container contains one <see cref="DataStore" /> for each registered cacheable type
    /// </summary>
    public class DataContainer
    {

        private readonly Services _serviceContainer;

        private Dictionary<string, int> _lastIdByGeneratorName = new Dictionary<string, int>();

        
        public DataContainer(Services serviceContainer, NodeConfig _config)
        {

            _serviceContainer = serviceContainer;
            Config = serviceContainer.NodeConfig;
        }

        /// <summary>
        ///     <see cref="DataStore" /> by <see cref="CollectionSchema" />
        /// </summary>
        private SafeDictionary<string, DataStore> DataStores { get; } = new SafeDictionary<string, DataStore>(null);


        public long ActiveConnections { private get; set; }

        public DateTime StartTime { private get; set; }


        public PersistenceEngine PersistenceEngine { private get; set; }

        public int ShardIndex { get; set; }

        private int ShardCount { get; set; }
        
        public INodeConfig Config { get; }
        public bool IsReadOnly { get; internal set; }

       

        /// <summary>
        ///     Dispatch the request to the appropriate consumer.
        ///     If it is a <see cref="DataRequest" /> dispatch it to its target
        ///     <see cref="DataStore" /> according to its CollectionName property
        ///     If it is an administrative request process it directly.
        /// </summary>
        /// <param name="clientRequest"></param>
        /// <param name="client"></param>
        public void DispatchRequest(Request clientRequest, IClient client)
        {
            if (clientRequest == null)
                throw new ArgumentNullException(nameof(clientRequest));
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            try
            {

                if (clientRequest is LockRequest lockRequest)
                {
                    ProcessLockRequest(lockRequest, client);
                    return;
                }

                if (clientRequest is DataRequest dataRequest)
                {
                    ProcessDataRequest(dataRequest, client);
                    return;
                }


                if (clientRequest.RequestClass == RequestClass.Admin)
                {
                    ProcessAdminRequest(clientRequest, client);
                    return;
                }


                if (clientRequest.RequestClass == RequestClass.UniqueIdGeneration)
                {
                    ProcessUniqueIdRequest(clientRequest, client);
                    return;
                }

                if (clientRequest is TransactionRequest transactionRequest)
                {
                    //TODO temporary: move to service container

                    var transactionManager = new TransactionManager(_serviceContainer.LockManager, PersistenceEngine);

                    transactionManager.ProcessTransactionRequest(transactionRequest, client, DataStores);

                    return;
                }

                throw new NotSupportedException(
                    $"An unknown request type was received {clientRequest.GetType().FullName}");
            }
            catch (Exception ex)
            {
                client.SendResponse(new ExceptionResponse(ex));
            }
        }

        private void ProcessLockRequest(LockRequest lockRequest, IClient client)
        {
            var lockManager = _serviceContainer.LockManager;

            try
            {
                if (lockRequest.Unlock)
                {
                    lockManager.CloseSession(lockRequest.SessionId);
                    client.SendResponse(new LockResponse { Success = true });
                }
                else
                {
                    bool lockAcquired = lockManager.TryAcquireReadLock(lockRequest.SessionId, Constants.DelayForLockInMilliseconds,
                        lockRequest.CollectionsToLock.ToArray());

                    client.SendResponse(new LockResponse { Success = lockAcquired });
                }
            }
            catch (Exception e)
            {
                client.SendResponse(new ExceptionResponse(e));
            }

        }



        public DataStore TryGetByName(string name)
        {
            return DataStores.TryGetValue(name);
        }

        public ICollection<DataStore> Stores(IList<string> types = null)
        {
            if (types == null)
                return DataStores.Values;

            return DataStores.Values.Where(ds => types.Contains(ds.CollectionSchema.CollectionName)).ToList();
        }


        /// <summary>
        ///     Creates the associated <see cref="DataStore" /> for new cacheable type
        /// </summary>
        /// <param name="request"></param>
        /// <param name="client"></param>
        private void RegisterType(RegisterTypeRequest request, IClient client)
        {

            try
            {
                var typeDescription = request.CollectionSchema;

                var collectionName = typeDescription.CollectionName;

                var dataStore = DataStores.TryGetValue(collectionName);


                if (ShardCount == 0) // not initialized
                {
                    ShardIndex = request.ShardIndex;
                    ShardCount = request.ShardsInCluster;
                }
                else // check it didn't change
                {
                    if (ShardIndex != request.ShardIndex || ShardCount != request.ShardsInCluster)
                        throw new NotSupportedException(
                            $"Cluster configuration changed. This node was shard {ShardIndex} / {ShardCount} and now is {request.ShardIndex} / {request.ShardsInCluster}");
                }

                if (dataStore != null) //type already registered
                {
                    //if schema changed reindex if the new one is more complex
                    var compatibility = CollectionSchema.AreCompatible(typeDescription, dataStore.CollectionSchema);
                    if(compatibility != CollectionSchema.CompatibilityLevel.Ok)                    
                    {

                        var newDataStore = DataStore.Reindex(dataStore, typeDescription, compatibility == CollectionSchema.CompatibilityLevel.NeedsRepacking);

                        DataStores[collectionName] = newDataStore;

                        _serviceContainer.SchemaPersistence.SaveSchema(GenerateSchema());

                    }
                }
                else // new type, store it in the type dictionary and initialize its DataStore
                {

                    var newDataStore =
                        new DataStore(typeDescription, new NullEvictionPolicy(), _serviceContainer.NodeConfig.FullTextConfig);


                    DataStores.Add(collectionName, newDataStore);

                    _serviceContainer.SchemaPersistence.SaveSchema(GenerateSchema());
                }

                client?.SendResponse(new NullResponse());
            }
            catch (Exception e)
            {
                client?.SendResponse(new ExceptionResponse(e));
            }
        }


        private void ProcessDataRequest(DataRequest dataRequest, IClient client)
        {
            Dbg.Trace($"data request for session {dataRequest.SessionId}");

            // the collection name is case insensitive
            var key = DataStores.Keys.FirstOrDefault(k => dataRequest.CollectionName.ToLower() == k.ToLower());

            DataStore dataStore = null;
            if (key != null)
            {
                dataStore = DataStores.TryGetValue(key);
            }
            else
            {
                // for now there is one special table containing the activity log. It is always non persistent and it has an LRU eviction policy

                if (dataRequest.CollectionName.ToLower() == "@activity")
                {
                    dataStore = _serviceContainer.Log.ActivityTable;
                }
            }


            if (dataStore == null)
            {
                throw new NotSupportedException("Unknown collection : " + dataRequest.CollectionName);
            }


            Dbg.Trace($"begin processing {dataRequest.AccessType} request on server {ShardIndex}");

            var lockManager = _serviceContainer.LockManager;

            if (dataRequest.AccessType == DataAccessType.Write)
            {
                if (dataRequest is DomainDeclarationRequest)
                    if (PersistenceEngine != null)
                        throw new NotSupportedException(
                            "Domain declaration can only be used in cache mode (without persistence)");

                if (dataRequest is EvictionSetupRequest)
                    if (PersistenceEngine != null)
                        throw new NotSupportedException(
                            "Eviction can only be used in cache mode (without persistence)");


                lockManager.DoWithWriteLock(() =>
                {
                    if (dataRequest is RemoveManyRequest removeManyRequest)
                    {
                        var mgr = new DeleteManager(dataStore, PersistenceEngine);

                        mgr.ProcessRequest(removeManyRequest, client);
                    }
                    else if (dataRequest is PutRequest putRequest)
                    {
                        var mgr = new PutManager(PersistenceEngine, _serviceContainer.FeedSessionManager, dataStore, _serviceContainer.Log);

                        mgr.ProcessRequest(putRequest, client);
                    }
                    else if (dataRequest is DomainDeclarationRequest domainDeclarationRequest)
                    {
                        var mgr = new CacheOnlyManager(dataStore);

                        mgr.ProcessRequest(domainDeclarationRequest, client);

                    }

                    else if (dataRequest is EvictionSetupRequest evictionSetupRequest)
                    {
                        var mgr = new CacheOnlyManager(dataStore);

                        mgr.ProcessRequest(evictionSetupRequest, client);

                    }

                }, dataRequest.CollectionName);

            }
            else
            {
                if (dataRequest.SessionId != default)// request inside a consistent read context
                {
                    if (lockManager.CheckLock(dataRequest.SessionId, false, dataRequest.CollectionName))
                    {
                        if (dataRequest is GetRequest getRequest)
                        {
                            new QueryManager(dataStore, _serviceContainer.Log).ProcessRequest(getRequest, client);
                        }
                        else if (dataRequest is EvalRequest evalRequest)
                        {
                            new QueryManager(dataStore, _serviceContainer.Log).ProcessRequest(evalRequest, client);
                        }
                        else if (dataRequest is PivotRequest pivotRequest)
                        {
                            new QueryManager(dataStore).ProcessRequest(pivotRequest, client);
                        }

                    }
                    else
                    {
                        throw new NotSupportedException("Data request with session received but no session is active");
                    }
                }
                else // simple request
                {
                    lockManager.DoWithReadLock(() =>
                    {

                        if (dataRequest is GetRequest getRequest)
                        {
                            new QueryManager(dataStore, _serviceContainer.Log).ProcessRequest(getRequest, client);
                        }
                        else if (dataRequest is EvalRequest evalRequest)
                        {
                            new QueryManager(dataStore, _serviceContainer.Log).ProcessRequest(evalRequest, client);
                        }
                        else if (dataRequest is PivotRequest pivotRequest)
                        {
                            new QueryManager(dataStore).ProcessRequest(pivotRequest, client);
                        }

                    }, dataRequest.CollectionName);

                }

            }

            Dbg.Trace($"end processing {dataRequest.AccessType} request on server {ShardIndex}");
        }


        /// <summary>
        ///     This kind of requests can be processed only in persistent mode (db not distributed cache)
        ///     This request is not related to data-stores so it is not processed by the scheduler
        /// </summary>
        /// <param name="clientRequest"></param>
        /// <param name="client"></param>
        private void ProcessUniqueIdRequest(Request clientRequest, IClient client)
        {
            

            switch (clientRequest)
            {
                case GenerateUniqueIdsRequest { Count: > 0 } generateUniqueIds
                    when !string.IsNullOrEmpty(generateUniqueIds.Name):
                         

                    // if we generate ids on an empty schema these values are not yet initialized
                    ShardIndex = generateUniqueIds.ShardIndex;
                    ShardCount = generateUniqueIds.ShardCount;


                    lock (_lastIdByGeneratorName)
                    {
                        //initialize a new sequence that starts at ShardIndex
                        if (!_lastIdByGeneratorName.ContainsKey(generateUniqueIds.Name))
                            _lastIdByGeneratorName[generateUniqueIds.Name] = ShardIndex;

                        var ids = new List<int>();


                        // we alter the sequences in memory before persisting them. Not an issue because they will not be sent to the 
                        // client if there is an exception during persistence. Just some wasted ids
                        for (var i = 0; i < generateUniqueIds.Count; i++)
                        {
                            var newId = _lastIdByGeneratorName[generateUniqueIds.Name] += ShardCount;
                            ids.Add(newId);
                        }

                        try
                        {
                            _serviceContainer.SequencePersistence.SaveValues(_lastIdByGeneratorName);

                            // return a response only if persisted successfully
                            client.SendResponse(new GenerateUniqueIdsResponse(ids.ToArray()));
                        }
                        catch (Exception e)
                        {
                            client.SendResponse(new ExceptionResponse(e));
                        }
                    }

                    break;

                case ResyncUniqueIdsRequest resync:

                    // if we generate ids on an empty schema these values are not yet initialized
                    ShardIndex = resync.ShardIndex;
                    ShardCount = resync.ShardCount;

                    foreach (var value in resync.NewStartValues)
                        _lastIdByGeneratorName[value.Key] = value.Value + resync.ShardIndex;

                    try
                    {

                        _serviceContainer.SequencePersistence.SaveValues(_lastIdByGeneratorName);

                        // return a response only if persisted successfully
                        client.SendResponse(new NullResponse());
                    }
                    catch (Exception e)
                    {
                        client.SendResponse(new ExceptionResponse(e));
                    }

                    break;


                default:
                    client.SendResponse(new ExceptionResponse(new CacheException("Invalid request")));
                    break;
            }
        }


        /// <summary>
        ///     Backup all data stores, schema and unique value generators to a directory
        /// </summary>
        /// <param name="request"></param>
        /// <param name="client"></param>
        private void Dump(DumpRequest request, IClient client)
        {
            if (DataStores.Count == 0)
            {
                throw new CacheException("Can not backup an empty database");
            }

            var lockManager = _serviceContainer.LockManager;

            // acquire a read lock on all the data stores
            lockManager.DoWithReadLock(() =>
            {
                InternalDump(request, client);
            }, DataStores.Keys.ToArray());


        }

        private void InternalDump(DumpRequest request, IClient client)
        {
            try
            {
                
                var cluster = _serviceContainer.NodeConfig.ClusterName;
                // a sub directory for each date is created inside the dump path
                var date = DateTime.Now;

                var directory = $"{date.ToString("yyyy-MM-dd")}_{date.ToString("HH")}h{date.ToString("mm")}_{ShardCount:D2}nodes_{cluster}";
                
                var fullPath = Path.Combine(request.Path, directory);

                if (!Directory.Exists(fullPath))
                    try
                    {
                        Directory.CreateDirectory(fullPath);
                    }
                    catch (Exception)
                    {
                        // ignore (race condition between nodes)
                    }

                Parallel.ForEach(DataStores.Values, ds => ds.Dump(fullPath, ShardIndex));


                // only the first node in the cluster should dump the schema as all shards have identical copies
                if (request.ShardIndex == 0)
                {
                    var schema = GenerateSchema();

                    _serviceContainer.SchemaPersistence.SaveSchema(schema, fullPath);

                }

                // save the sequences. Each shard has different values

                var dumpSequenceFileName = $"sequence_{ShardIndex:D3}.json";
                var sequencePath = Path.Combine(fullPath, dumpSequenceFileName);

                _serviceContainer.SequencePersistence.SaveValues(_lastIdByGeneratorName, sequencePath);

                client.SendResponse(new NullResponse());
            }
            catch (AggregateException agg)
            {
                client.SendResponse(new ExceptionResponse(agg.InnerExceptions.First()));
            }
            catch (Exception e)
            {
                client.SendResponse(new ExceptionResponse(e));
            }
        }



        private void GetKnownTypes(IClient client)
        {
            try
            {
                var response = new ServerDescriptionResponse();

                var stores = DataStores.Values;



                foreach (var store in stores)
                {
                    response.AddTypeDescription(store.CollectionSchema);


                    var info = new DataStoreInfo
                    {
                        Count = store.DataByPrimaryKey.Count,
                        EvictionPolicy = store.EvictionType,
                        EvictionPolicyDescription =
                            store.EvictionPolicy.ToString(),
                        FullTypeName = store.CollectionSchema.CollectionName,
                        AvailableData =
                            store.DomainDescription ??
                            new DomainDescription(null),
                        StorageLayout = store.CollectionSchema.StorageLayout,

                        HitCount = store.HitCount,
                        ReadCount = store.ReadCount
                    };

                    response.AddDataStoreInfo(info);
                }


                // add the special @ACTIVITY table (it may not be initialized in test environments)
                if (_serviceContainer.Log != null)
                {
                    var activityInfo = new DataStoreInfo
                    {
                        FullTypeName = LogEntry.Table,
                        Count = _serviceContainer.Log.ActivityTable.DataByPrimaryKey.Count,
                        EvictionPolicy = _serviceContainer.Log.ActivityTable.EvictionType
                    };

                    response.AddDataStoreInfo(activityInfo);

                    response.AddTypeDescription(_serviceContainer.Log.ActivityTable.CollectionSchema);
                }


                var currentProcess = Process.GetCurrentProcess();

                var assembly = Assembly.GetAssembly(typeof(Server));
                response.ServerProcessInfo = new ServerInfo
                {
                    TransactionLag = PersistenceEngine?.PendingTransactions ?? 0,
                    ConnectedClients = (int)ActiveConnections,
                    StartTime = StartTime,
                    Bits = IntPtr.Size * 8,
                    Threads = currentProcess.Threads.Count,
                    WorkingSet = currentProcess.WorkingSet64,
                    VirtualMemory = currentProcess.VirtualMemorySize64,
                    IsPersistent = Config.IsPersistent,
                    Host = Environment.MachineName,
                    Port = Config.TcpPort,
                    MemoryLimitInGigabytes = Config.MemoryLimitInGigabytes,
                    IsReadOnly = IsReadOnly,
                    ClusterName = Config.ClusterName,
                    SoftwareVersion =
                        assembly != null
                            ? assembly.GetName().Version.ToString()
                            : ""
                };


                client.SendResponse(response);
            }
            catch (Exception ex)
            {
                client.SendResponse(new ExceptionResponse(ex));
            }
        }

        private void ProcessAdminRequest(Request clientRequest, IClient client)
        {
            if (clientRequest is RegisterTypeRequest req)
            {
                RegisterType(req, client);
                return;
            }

            if (clientRequest is GetKnownTypesRequest)
            {
                GetKnownTypes(client);

                return;
            }


            // This one is special. A short lock is required as even the read-only operations write in the ServerLog
            // The lock is inside ServerLog no need to use the scheduler
            if (clientRequest is LogRequest logRequest)
            {
                try
                {
                    var lines = logRequest.LinesCount;

                    var entries = ServerLog.GetLast(lines);
                    var maxLockEntry = ServerLog.MaxLogEntry;

                    var response = new LogResponse();
                    foreach (var entry in entries) response.Entries.Add(entry);

                    response.MaxLockEntry = maxLockEntry;

                    client.SendResponse(response);
                }
                catch (Exception ex)
                {
                    client.SendResponse(new ExceptionResponse(ex));
                }

                return;
            }

            if (clientRequest is DumpRequest dumpRequest)
            {
                Dump(dumpRequest, client);
                return;
            }


            throw new NotSupportedException("Unknown request type: " + clientRequest.GetType());
        }

        public Schema GenerateSchema()
        {

            var collectionsDescriptions = new Dictionary<string, CollectionSchema>();

            foreach (var store in DataStores.Pairs)
            {
                collectionsDescriptions.Add(store.Key, store.Value.CollectionSchema);
            }

            return new Schema
            { ShardIndex = ShardIndex, ShardCount = ShardCount, CollectionsDescriptions = collectionsDescriptions };


        }

        public void LoadSchema(string path)
        {
            var schema = _serviceContainer.SchemaPersistence.LoadSchema(path);

            if (schema != null)
            {
                foreach (var description in schema.CollectionsDescriptions)
                {
                    ServerLog.LogInfo($"declaring collection {description.Key}");
                    RegisterType(new RegisterTypeRequest(description.Value, schema.ShardIndex, schema.ShardCount), null);
                }

                ShardIndex = schema.ShardIndex;
                ShardCount = schema.ShardCount;
            }
        }

        public void LoadSequence(string path)
        {

            _lastIdByGeneratorName = _serviceContainer.SequencePersistence.LoadValues(path) ?? new Dictionary<string, int>();
        }
    }
}