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
        /// <summary>
        ///     <see cref="DataStore" /> by TypeDescription
        /// </summary>
        private readonly Dictionary<string, DataStore> _dataStores;

        /// <summary>
        ///     List of registered types (as <see cref="TypeDescription" />) indexed by
        ///     fullTypeName
        /// </summary>
        private readonly Dictionary<string, TypeDescription> _knownTypes;


        private readonly JsonSerializerSettings _schemaSerializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented
        };

        private readonly JsonSerializer _jsonSerializer;

        
        private Dictionary<string, int> _lastIdByGeneratorName = new Dictionary<string, int>();

        public DataContainer(ServerConfig config, Profiler profiler)
        {
            Profiler = profiler;

            _dataStores = new Dictionary<string, DataStore>();
            _knownTypes = new Dictionary<string, TypeDescription>();

            _jsonSerializer = JsonSerializer.Create(_schemaSerializerSettings);
            _jsonSerializer.Converters.Add(new StringEnumConverter());


            Config = config;
            Config = config;

           
            
        }

        public void StartProcessingClientRequests()
        {
            
        }

        public void Stop()
        {
            
        }
        
        /// <summary>
        ///     <see cref="DataStore" /> by <see cref="TypeDescription" />
        /// </summary>
        public IDictionary<string, DataStore> DataStores => _dataStores;

        public long ActiveConnections { private get; set; }

        public DateTime StartTime { private get; set; }

        public ServerConfig Config { get; }

        private Profiler Profiler { get; }

        public PersistenceEngine PersistenceEngine { private get; set; }
        public int ShardIndex { get; set; }

        public int ShardCount { get; set; }

        /// <summary>
        ///     Dispatch the request to the appropriate consumer.
        ///     If it is a <see cref="DataRequest" /> dispatch it to its target
        ///     <see cref="DataStore" /> according to its FullTypeName property
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

            var typesToPut = transactionRequest.ItemsToPut.Select(i => i.FullTypeName).Distinct().ToList();
            var typesToDelete = transactionRequest.ItemsToDelete.Select(i => i.FullTypeName).Distinct().ToList();
            var types = typesToPut.Union(typesToDelete).Distinct().ToList();

            if (types.Any(t => !DataStores.ContainsKey(t))) throw new NotSupportedException("Type not registered");


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
                int index = 0;
                foreach (var condition in transactionRequest.Conditions)
                {
                    if (!condition.IsEmpty())
                    {
                        var ds = DataStores[condition.TypeName];

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
                foreach (var type in types)
                    if (DataStores[type].Lock.IsWriteLockHeld)
                        DataStores[type].Lock.ExitWriteLock();
                return;
            }
            catch (Exception e)
            {
                Dbg.Trace($"error in first stage:{e.Message} server {ShardIndex}");
                client.SendResponse(new ExceptionResponse(e));
                // failed to write a durable transaction so stop here
                
                // unlock
                foreach (var type in types)
                    if (DataStores[type].Lock.IsWriteLockHeld)
                        DataStores[type].Lock.ExitWriteLock();
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
                            if (!DataStores.ContainsKey(dataRequest.FullTypeName))
                                throw new NotSupportedException(
                                    $"The type {dataRequest.FullTypeName} is not registered");
                            DataStores[dataRequest.FullTypeName].ProcessRequest(dataRequest, client, null);
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
            foreach (var type in types)
                if (DataStores[type].Lock.IsWriteLockHeld)
                    DataStores[type].Lock.ExitWriteLock();
        }

        private void ProcessSingleStageTransactionRequest(TransactionRequest transactionRequest, IClient client)
        {
            var typesToPut = transactionRequest.ItemsToPut.Select(i => i.FullTypeName).Distinct().ToList();
            var typesToDelete = transactionRequest.ItemsToDelete.Select(i => i.FullTypeName).Distinct().ToList();
            var types = typesToPut.Union(typesToDelete).Distinct();


            try
            {

                foreach (var type in types)
                    if (!DataStores[type].Lock.TryEnterWriteLock(Constants.DelayForLock))
                    {
                        throw new CacheException("Failed to acquire write locks for single-stage transaction",
                            ExceptionType.FailedToAcquireLock);
                    }

                Dbg.Trace($"S: fallback to single stage for transaction {transactionRequest.TransactionId}");


                // check the conditions (in case of conditional update)
                int index = 0;
                foreach (var condition in transactionRequest.Conditions)
                {
                    if (!condition.IsEmpty())
                    {
                        var ds = DataStores[condition.TypeName];

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
                    if (!DataStores.ContainsKey(dataRequest.FullTypeName))
                        throw new NotSupportedException($"The type {dataRequest.FullTypeName} is not registered");

                    DataStores[dataRequest.FullTypeName].ProcessRequest(dataRequest, client, null);
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
            finally
            {
                // release acquired locks
                foreach (var type in types)
                    if (DataStores[type].Lock.IsWriteLockHeld)
                        DataStores[type].Lock.ExitWriteLock();
                
            }
        }

        private bool AcquireWriteLock(IClient client, List<string> types, string transactionId)
        {
            var result = true;

            foreach (var type in types)
                if (!DataStores[type].Lock.TryEnterWriteLock(Constants.DelayForLock))
                {
                    result = false;
                    break;
                }

            if (result)
            {
                Dbg.Trace($"S: Locks acquired on server {ShardIndex}  transaction {transactionId}");

                //client.SendResponse(new ReadyResponse());

                var answer = client.ShouldContinue();
                if (answer.HasValue && answer.Value)
                {
                    Dbg.Trace(
                        $"S: all clients acquired locks. Continue on server {ShardIndex}  transaction {transactionId}");

                    return true;
                }
            }

            Dbg.Trace($"S: Not all clients acquired locks on server {ShardIndex}  transaction {transactionId}.");

            // first unlock everything to avoid deadlocks
            foreach (var type in types)
                if (DataStores[type].Lock.IsWriteLockHeld)
                    DataStores[type].Lock.ExitWriteLock();

            Dbg.Trace($"S: Release all locks on server {ShardIndex}  transaction {transactionId}.");

            client.SendResponse(new ExceptionResponse(
                new CacheException(
                    $"can not acquire write lock on server {ShardIndex}  for transaction {transactionId}"),ExceptionType.FailedToAcquireLock));

            return false;
        }

        /// <summary>
        ///     Creates the associated <see cref="DataStore" /> for new cacheable type
        /// </summary>
        /// <param name="request"></param>
        /// <param name="client"></param>
        private void RegisterType(RegisterTypeRequest request, IClient client)
        {
            if (client != null) // client request
                ProcessRegisterType(request, client);
            else // called internally (while loading data)
                InternalProcessRegisterType(request);
        }
        

        private void ProcessRegisterType(RegisterTypeRequest request, IClient client)
        {
            lock (DataStores)
            {
                try
                {
                    InternalProcessRegisterType(request);
                    client.SendResponse(new NullResponse());
                }
                catch (Exception e)
                {
                    client.SendResponse(new ExceptionResponse(e));
                }
            }
        }


        private void InternalProcessRegisterType(RegisterTypeRequest request)
        {
            var typeDescription = request.TypeDescription;

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

            if (_knownTypes.ContainsKey(typeDescription.FullTypeName)) //type already registered
            {
                //check that the type description is the same
                if (!typeDescription.Equals(_knownTypes[typeDescription.FullTypeName]))
                {
                    var message =
                        $"The type {typeDescription.FullTypeName} is already registered but the type description is different";
                    throw new NotSupportedException(message);
                }
            }
            else // new type, store it in the type dictionary and initialize its DataStore
            {
                _knownTypes.Add(typeDescription.FullTypeName, typeDescription);

                PersistenceEngine?.UpdateSchema(GenerateSchema());


                var cfg = Config.ConfigByType.ContainsKey(typeDescription.FullTypeName)
                    ? Config.ConfigByType[typeDescription.FullTypeName]
                    : new ServerDatastoreConfig();

                var evictionPolicy = EvictionPolicyFactory.CreateEvictionPolicy(cfg.Eviction);

                var newDataStore =
                    new DataStore(typeDescription, evictionPolicy);


                Dbg.CheckThat(Profiler != null);

                newDataStore.Profiler = Profiler;
                DataStores.Add(typeDescription.FullTypeName, newDataStore);
            }
        }


        private void PersistTransaction(Transaction transaction)
        {
            PersistenceEngine?.NewTransaction(transaction);
        }


        private void ProcessDataRequest(DataRequest dataRequest, IClient client)
        {
            DataStore dataStore;
            lock (DataStores)
            {
                var fullTypeName = dataRequest.FullTypeName;

                if (!DataStores.ContainsKey(fullTypeName))
                    throw new NotSupportedException("Not registered type : " + fullTypeName);

                dataStore = DataStores[fullTypeName];
            }

            Dbg.Trace($"begin processing {dataRequest.AccessType} request on server {ShardIndex}");

            if (dataRequest.AccessType == DataAccessType.Write)
            {
                if (dataRequest is DomainDeclarationRequest)
                {
                    if (PersistenceEngine != null)
                    {
                        throw new NotSupportedException("Domain declaration can only be used in cache mode (without persistence)");
                    }
                }

                if (dataRequest is EvictionSetupRequest)
                {
                    if (PersistenceEngine != null)
                    {
                        throw new NotSupportedException("Eviction can only be used in cache mode (without persistence)");
                    }
                }


                if (dataStore.Lock.TryEnterWriteLock(-1))
                    try
                    {
                        dataStore.ProcessRequest(dataRequest, client, PersistTransaction);
                    }
                    finally
                    {
                        dataStore.Lock.ExitWriteLock();
                    }
                else
                    Dbg.Trace(
                        $"failed to acquire read-only lock on server {ShardIndex} for type {dataRequest.FullTypeName}");
            }
            else
            {
                if (dataStore.Lock.TryEnterReadLock(-1))
                    try
                    {
                        dataStore.ProcessRequest(dataRequest, client, PersistTransaction);
                    }
                    finally
                    {
                        dataStore.Lock.ExitReadLock();
                    }
                else
                    Dbg.Trace(
                        $"failed to acquire write lock on server {ShardIndex} for type {dataRequest.FullTypeName}");
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
            lock (DataStores)
            {
                try
                {
                    foreach (var dataStore in DataStores.Values)
                        if (!dataStore.Lock.TryEnterReadLock(Constants.DelayForLock))
                            throw new NotSupportedException(
                                $"Can not acquire read-only lock for type {dataStore.TypeDescription.FullTypeName} on server {ShardIndex}");

                    InternalDump(request, client);
                }
                finally
                {
                    foreach (var dataStore in DataStores.Values)
                        if (dataStore.Lock.IsReadLockHeld)
                            dataStore.Lock.ExitReadLock();
                }
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

                Parallel.ForEach(_dataStores, ds => ds.Value.Dump(request, ShardIndex));


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
            lock (DataStores)
            {
                InternalGetKnownTypes(client);
            }
        }

        private void InternalGetKnownTypes(IClient client)
        {
            try
            {
                var response = new ServerDescriptionResponse();


                foreach (var pair in _dataStores)
                {
                    response.AddTypeDescription(pair.Value.TypeDescription);

                    var dataStore = pair.Value;

                    var info = new DataStoreInfo
                    {
                        Count = dataStore.Count,
                        EvictionPolicy = dataStore.EvictionType,
                        EvictionPolicyDescription =
                            dataStore.EvictionPolicy.ToString(),
                        FullTypeName = dataStore.TypeDescription.FullTypeName,
                        AvailableData =
                            dataStore.DomainDescription ??
                            new DomainDescription(null),
                        DataCompression = dataStore.TypeDescription.UseCompression,

                        HitCount = dataStore.HitCount,
                        ReadCount = dataStore.ReadCount
                    };

                    response.AddDataStoreInfo(info);
                }


                var currentProcess = Process.GetCurrentProcess();

                var assembly = Assembly.GetEntryAssembly();
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
            var schema = new Schema
                {ShardIndex = ShardIndex, ShardCount = ShardCount, TypeDescriptions = _knownTypes.Values.ToList()};
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
                foreach (var typeDescription in schema.TypeDescriptions)
                {
                    ServerLog.LogInfo($"registering type {typeDescription.FullTypeName}");
                    RegisterType(new RegisterTypeRequest(typeDescription, schema.ShardIndex, schema.ShardCount), null);
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