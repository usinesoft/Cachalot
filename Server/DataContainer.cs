#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Client;
using Client.ChannelInterface;
using Client.Core;
using Client.Interface;
using Client.Messages;
using Client.Profiling;
using Client.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Server.Persistence;

#endregion

namespace Server
{
    /// <summary>
    ///     The data container contains one <see cref="DataStore" /> for each registered cacheable type
    /// </summary>
    public class DataContainer
    {
        private readonly NodeConfig _config;
        private readonly Services _serviceContainer;


        private readonly JsonSerializer _jsonSerializer;


        private readonly JsonSerializerSettings _schemaSerializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented
        };


        private Dictionary<string, int> _lastIdByGeneratorName = new Dictionary<string, int>();

        public DataContainer(Profiler profiler, NodeConfig config, Services serviceContainer)
        {
            _config = config;
            _serviceContainer = serviceContainer;

            Profiler = profiler;

            _jsonSerializer = JsonSerializer.Create(_schemaSerializerSettings);
            _jsonSerializer.Converters.Add(new StringEnumConverter());
        }

        /// <summary>
        ///     <see cref="DataStore" /> by <see cref="CollectionSchema" />
        /// </summary>
        private SafeDictionary<string, DataStore> DataStores {get;} = new SafeDictionary<string, DataStore>(null);


        public long ActiveConnections { private get; set; }

        public DateTime StartTime { private get; set; }


        private Profiler Profiler { get; }

        public PersistenceEngine PersistenceEngine { private get; set; }

        public int ShardIndex { get; set; }

        private int ShardCount { get; set; }

        public void StartProcessingClientRequests()
        {
        }

        public void Stop()
        {
        }

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
                    ProcessTransactionRequest(transactionRequest, client);
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


        private void ProcessTransactionRequest(TransactionRequest transactionRequest, IClient client)
        {
            // First try to acquire a write lock on all concerned data stores

            var typesToPut = transactionRequest.ItemsToPut.Select(i => i.CollectionName).Distinct().ToList();
            var typesToDelete = transactionRequest.ItemsToDelete.Select(i => i.CollectionName).Distinct().ToList();
            var types = typesToPut.Union(typesToDelete).Distinct().ToList();


            var keys = DataStores.Keys;
            
            if (types.Any(t => !keys.Contains(t))) throw new NotSupportedException("Type not registered");
            


            // do not work too hard if it's single stage
            if (transactionRequest.IsSingleStage)
            {
                ProcessSingleStageTransactionRequest(transactionRequest, client);
                return;
            }


            if (!AcquireWriteLock(client, types, transactionRequest.TransactionId)) return;

            Dbg.Trace($"S: lock acquired by all clients for transaction {transactionRequest.TransactionId}");


            // Second register a durable delayed transaction. It can be cancelled later

            try
            {
                // check the conditions (in case of conditional update)
                var index = 0;
                foreach (var condition in transactionRequest.Conditions)
                {
                    if (!condition.IsEmpty())
                    {
                        var ds = DataStores[condition.CollectionName];

                        ds.CheckCondition(transactionRequest.ItemsToPut[index].PrimaryKey, condition);
                    }

                    index++;
                }

                Dbg.Trace($"S: begin writing delayed transaction {transactionRequest.TransactionId}");
                PersistenceEngine?.NewTransaction(new MixedTransaction
                    {
                        ItemsToDelete = transactionRequest.ItemsToDelete,
                        ItemsToPut = transactionRequest.ItemsToPut
                    },
                    true
                );

                client.SendResponse(new ReadyResponse());


                Dbg.Trace($"S: end writing delayed transaction {transactionRequest.TransactionId}");
            }
            catch (CacheException e)
            {
                Dbg.Trace($"error in first stage:{e.Message} server {ShardIndex}");
                client.SendResponse(new ExceptionResponse(e, e.ExceptionType));
                // failed to write a durable transaction so stop here

                // unlock
                RemoveWriteLocks(types);
                return;
            }
            catch (Exception e)
            {
                Dbg.Trace($"error in first stage:{e.Message} server {ShardIndex}");
                client.SendResponse(new ExceptionResponse(e));
                // failed to write a durable transaction so stop here

                // unlock
                RemoveWriteLocks(types);
                
                return;
            }


            try
            {
                Dbg.Trace($"S: begin waiting for client go {transactionRequest.TransactionId}");
                var answer = client.ShouldContinue();
                Dbg.Trace($"S: end waiting for client go answer = {answer}");

                if (answer.HasValue) // the client has answered
                {
                    if (answer.Value)
                    {
                        // update the data in memory
                        var dataRequests = transactionRequest.SplitByType();

                        foreach (var dataRequest in dataRequests)
                        {
                            if (!DataStores.Keys.Contains(dataRequest.FullTypeName))
                                    throw new NotSupportedException(
                                        $"The type {dataRequest.FullTypeName} is not registered");
                                
                            var store = DataStores[dataRequest.FullTypeName];
                            
                            store.ProcessRequest(dataRequest, client, null);
                        }

                        ServerLog.LogInfo(
                            $"S: two stage transaction committed successfully on server {ShardIndex} {transactionRequest.TransactionId}");
                    }
                    else
                    {
                        ServerLog.LogWarning(
                            $"S: two stage transaction cancelled by client on server {ShardIndex} {transactionRequest.TransactionId}");

                        // cancel the delayed transaction
                        PersistenceEngine?.CancelDelayedTransaction();
                    }
                }
                else // the client failed to answer in a reasonable delay (which is less than the delay to commit a delayed transaction )
                {
                    PersistenceEngine?.CancelDelayedTransaction();
                }
            }
            catch (Exception e)
            {
                ServerLog.LogInfo($"error in the second stage of a transaction:{e.Message}");
            }


            // unlock
            RemoveWriteLocks(types);
        }

        private void ProcessSingleStageTransactionRequest(TransactionRequest transactionRequest, IClient client)
        {
            var typesToPut = transactionRequest.ItemsToPut.Select(i => i.CollectionName).Distinct().ToList();
            var typesToDelete = transactionRequest.ItemsToDelete.Select(i => i.CollectionName).Distinct().ToList();
            var types = typesToPut.Union(typesToDelete).Distinct().ToArray();


            var lockManager = _serviceContainer.LockManager;


            lockManager.DoIfWriteLock(() =>
            {
                try
                {
                    Dbg.Trace($"S: fallback to single stage for transaction {transactionRequest.TransactionId}");


                    // check the conditions (in case of conditional update)
                    var index = 0;
                    foreach (var condition in transactionRequest.Conditions)
                    {
                        if (!condition.IsEmpty())
                        {
                            var ds = DataStores[condition.CollectionName];

                            ds.CheckCondition(transactionRequest.ItemsToPut[index].PrimaryKey, condition);
                        }

                        index++;
                    }


                    PersistenceEngine?.NewTransaction(new MixedTransaction
                        {
                            ItemsToDelete = transactionRequest.ItemsToDelete,
                            ItemsToPut = transactionRequest.ItemsToPut
                        }
                    );

                    // update the data in memory
                    var dataRequests = transactionRequest.SplitByType();

                    foreach (var dataRequest in dataRequests)
                    {
                        var ds = DataStores.TryGetValue(dataRequest.FullTypeName);


                        if (ds == null)
                            throw new NotSupportedException($"The type {dataRequest.FullTypeName} is not registered");


                        ds.ProcessRequest(dataRequest, client, null);
                    }

                    client.SendResponse(new NullResponse());

                    Dbg.Trace($"S: end writing delayed transaction {transactionRequest.TransactionId}");
                }
                catch (CacheException e)
                {
                    client.SendResponse(new ExceptionResponse(e, e.ExceptionType));
                }
                catch (Exception e)
                {
                    Dbg.Trace($"error writing delayed transaction:{e.Message}");
                    client.SendResponse(new ExceptionResponse(e));
                    // failed to write a durable transaction so stop here
                }
            }, Constants.DelayForLock, types);

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

        private bool AcquireWriteLock(IClient client, List<string> types, string transactionId)
        {

            var lockManager = _serviceContainer.LockManager;

            if (lockManager.TryAcquireWriteLock(Constants.DelayForLock, types.ToArray()))
            {
                Dbg.Trace($"S: Locks acquired on server {ShardIndex}  transaction {transactionId}");

                var answer = client.ShouldContinue();
                if (answer.HasValue && answer.Value)
                {
                    Dbg.Trace(
                        $"S: all clients acquired locks. Continue on server {ShardIndex}  transaction {transactionId}");

                    return true;
                }

                Dbg.Trace($"S: Not all clients acquired locks on server {ShardIndex}  transaction {transactionId}.");

                lockManager.RemoveWriteLock(types.ToArray());

                return false;
            }
            
            // not all the locks have been taken if we reach this point but some may be taken
            lockManager.RemoveWriteLock(types.ToArray());

            client.SendResponse(new ExceptionResponse(
                new CacheException(
                    $"can not acquire write lock on server {ShardIndex}  for transaction {transactionId}"),
                ExceptionType.FailedToAcquireLock));

            

            return false;

            
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

                // TODO temporary fallback
                var collectionName = request.CollectionName ?? typeDescription.CollectionName;

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
                    //if the type description changed reindex
                    if (!typeDescription.Equals(dataStore.CollectionSchema))
                    {
                      
                        var newDataStore = DataStore.Reindex(dataStore, typeDescription);

                        DataStores[collectionName] = newDataStore;

                        PersistenceEngine?.UpdateSchema(GenerateSchema());
                
                    }
                }
                else // new type, store it in the type dictionary and initialize its DataStore
                {
            
                    var newDataStore =
                        new DataStore(typeDescription, new NullEvictionPolicy(), _config);

                    Dbg.CheckThat(Profiler != null);

                    newDataStore.Profiler = Profiler;
                    DataStores.Add(collectionName, newDataStore);

                    PersistenceEngine?.UpdateSchema(GenerateSchema());
                }

                client?.SendResponse(new NullResponse());
            }
            catch (Exception e)
            {
                client?.SendResponse(new ExceptionResponse(e));
            }
        }

        void RemoveWriteLocks(IList<string> collections)
        {
            var lockManager = _serviceContainer.LockManager;

            lockManager.RemoveWriteLock(collections.ToArray());
        }


        private void PersistTransaction(Transaction transaction)
        {
            PersistenceEngine?.NewTransaction(transaction);
        }


        private void ProcessDataRequest(DataRequest dataRequest, IClient client)
        {
            DataStore dataStore = DataStores.TryGetValue(dataRequest.FullTypeName);

            if (dataStore == null)
            {
                throw new NotSupportedException("Not registered type : " + dataRequest.FullTypeName);
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
                    dataStore.ProcessRequest(dataRequest, client, PersistTransaction);
                }, dataRequest.FullTypeName);

            }
            else
            {
    
                lockManager.DoWithReadLock(() =>
                {
                    dataStore.ProcessRequest(dataRequest, client, PersistTransaction);
                },dataRequest.FullTypeName);

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
            if (PersistenceEngine == null)
            {
                client.SendResponse(
                    new ExceptionResponse(
                        new CacheException("Unique ids can be generated only by persistent servers")));
                return;
            }

            switch (clientRequest)
            {
                case GenerateUniqueIdsRequest generateUniqueIds
                    when generateUniqueIds.Count > 0 && !string.IsNullOrEmpty(generateUniqueIds.Name) &&
                         PersistenceEngine != null:

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
                            var sb = new StringBuilder();

                            _jsonSerializer.Serialize(new JsonTextWriter(new StringWriter(sb)), _lastIdByGeneratorName);

                            PersistenceEngine.UpdateSequences(sb.ToString());

                            // return a response only if persisted successfully
                            client.SendResponse(new GenerateUniqueIdsResponse(ids.ToArray()));
                        }
                        catch (Exception e)
                        {
                            client.SendResponse(new ExceptionResponse(e));
                        }
                    }

                    break;

                case ResyncUniqueIdsRequest resync when PersistenceEngine != null:

                    // if we generate ids on an empty schema these values are not yet initialized
                    ShardIndex = resync.ShardIndex;
                    ShardCount = resync.ShardCount;

                    foreach (var value in resync.NewStartValues)
                        _lastIdByGeneratorName[value.Key] = value.Value + resync.ShardIndex;

                    try
                    {
                        var sb = new StringBuilder();

                        _jsonSerializer.Serialize(new JsonTextWriter(new StringWriter(sb)), _lastIdByGeneratorName);

                        PersistenceEngine.UpdateSequences(sb.ToString());

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
        ///     Scheduled as read-only task
        /// </summary>
        /// <param name="request"></param>
        /// <param name="client"></param>
        private void Dump(DumpRequest request, IClient client)
        {
            
            var lockManager = _serviceContainer.LockManager;

            // acquire a read lock on all the data stores
            lockManager.DoWithReadLock(() =>
            {
                
            });

            var success = lockManager.DoIfWriteLock(() =>
            {
                InternalDump(request, client);
            }, Constants.DelayForLock, DataStores.Keys.ToArray());

            if (!success)
            {
                throw new NotSupportedException( "Can not acquire read-only lock for dump");
            }
                
            
        }

        private void InternalDump(DumpRequest request, IClient client)
        {
            try
            {
                // a sub directory for each date is created inside the dump path
                var date = DateTime.Today.ToString("yyyy-MM-dd");

                var fullPath = Path.Combine(request.Path, date);

                if (!Directory.Exists(fullPath))
                    try
                    {
                        Directory.CreateDirectory(fullPath);
                    }
                    catch (Exception)
                    {
                        // ignore (race condition between nodes)
                    }

                Parallel.ForEach(DataStores.Values, ds => ds.Dump(request, ShardIndex));


                // only the first node in the cluster should dump the schema as all shards have identical copies
                if (request.ShardIndex == 0)
                {
                    var schemaJson = GenerateSchema();

                    File.WriteAllText(Path.Combine(fullPath, Constants.SchemaFileName), schemaJson);
                }

                // save the sequences. Each shard has different values
                var sb = new StringBuilder();

                _jsonSerializer.Serialize(new JsonTextWriter(new StringWriter(sb)), _lastIdByGeneratorName);

                var dumpSequenceFileName = $"sequence_{ShardIndex:D3}.json";
                File.WriteAllText(Path.Combine(fullPath, dumpSequenceFileName), sb.ToString());


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
                        Count = store.Count,
                        EvictionPolicy = store.EvictionType,
                        EvictionPolicyDescription =
                            store.EvictionPolicy.ToString(),
                        FullTypeName = store.CollectionSchema.CollectionName,
                        AvailableData =
                            store.DomainDescription ??
                            new DomainDescription(null),
                        DataCompression = store.CollectionSchema.UseCompression,

                        HitCount = store.HitCount,
                        ReadCount = store.ReadCount
                    };

                    response.AddDataStoreInfo(info);
                }


                var currentProcess = Process.GetCurrentProcess();

                var assembly = Assembly.GetAssembly(typeof(Server));
                response.ServerProcessInfo = new ServerInfo
                {
                    ConnectedClients = (int) ActiveConnections,
                    StartTime = StartTime,
                    Bits = IntPtr.Size * 8,
                    Threads = currentProcess.Threads.Count,
                    WorkingSet = currentProcess.WorkingSet64,
                    VirtualMemory = currentProcess.VirtualMemorySize64,
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

        public string GenerateSchema()
        {
            
            var collectionsDescriptions = new Dictionary<string, CollectionSchema>();

            foreach (var store in DataStores.Values)
            {
                collectionsDescriptions.Add(store.CollectionSchema.CollectionName, store.CollectionSchema);
            }

            var schema = new Schema
                {ShardIndex = ShardIndex, ShardCount = ShardCount, CollectionsDescriptions = collectionsDescriptions};

            var sb = new StringBuilder();

            _jsonSerializer.Serialize(new JsonTextWriter(new StringWriter(sb)), schema);

            return sb.ToString();
            
        }

        public void LoadSchema(string path)
        {
            if (!File.Exists(path)) return;

            var json = File.ReadAllText(path);

            var schema = _jsonSerializer.Deserialize<Schema>(
                new JsonTextReader(new StringReader(json)));

            if (schema != null)
            {
                foreach (var description in schema.CollectionsDescriptions)
                {
                    ServerLog.LogInfo($"declaring collection {description.Key}");
                    RegisterType(new RegisterTypeRequest(description.Value, schema.ShardIndex, schema.ShardCount, description.Key), null);
                }

                ShardIndex = schema.ShardIndex;
                ShardCount = schema.ShardCount;
            }
        }

        public void LoadSequence(string path)
        {
            if (!File.Exists(path)) return;

            var json = File.ReadAllText(path);

            _lastIdByGeneratorName = _jsonSerializer.Deserialize<Dictionary<string, int>>(
                                         new JsonTextReader(new StringReader(json))) ?? new Dictionary<string, int>();
        }
    }
}