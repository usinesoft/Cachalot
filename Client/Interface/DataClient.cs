//#define  DEBUG_VERBOSE

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.ChannelInterface;
using Client.Core;
using Client.Messages;
using Client.Messages.Pivot;
using Client.Queries;
using Client.Tools;

namespace Client.Interface;

public sealed class DataClient : IDataClient
{
    public int ShardIndex { get; set; }

    public int ShardsCount { get; set; } = 1;

    /// <summary>
    ///     Informative only. In case of connection error
    /// </summary>
    public string ServerHostname { get; set; }

    public int ServerPort { get; set; }

    /// <summary>
    ///     In order to connect to a server, a client needs a channel for data transport.
    ///     Usually you do not need to explicitly instantiate an <see cref="IClientChannel" /> and connect it to the server.
    ///     Use a factory (like Cachalot.Channel.TCPClientFactory) to instantiate both the client and the channel
    ///     <example>
    ///         CacheClient client = TcpClientFactory.FromElement("CacheClientConfig.xml");
    ///     </example>
    /// </summary>
    public IClientChannel Channel { get; set; }

    public ClusterInformation GetClusterInformation()
    {
        var request = new GetKnownTypesRequest();

        try
        {
            var response = Channel.SendRequest(request);

            if (response is ExceptionResponse exResponse)
                throw new CacheException("Error while getting server information", exResponse.Message,
                    exResponse.CallStack);

            var concreteResponse = response as ServerDescriptionResponse;

            Dbg.CheckThat(concreteResponse != null);

            return new(new[] { concreteResponse });
        }
        catch (Exception)
        {
            var response = new ServerDescriptionResponse
            {
                ConnectionError = true,
                ServerProcessInfo = new() { Host = ServerHostname, Port = ServerPort }
            };

            return new(new[] { response });
        }
    }

    public ServerLog GetLog(int lastLines)
    {
        var request = new LogRequest(lastLines);

        var response = Channel.SendRequest(request);

        if (response is ExceptionResponse exResponse)
            throw new CacheException("Error while retrieving server log", exResponse.Message, exResponse.CallStack);

        return new(new[] { response as LogResponse });
    }

    public void SetReadonlyMode(bool rw = false)
    {
        var request = new SwitchModeRequest(rw ? 0 : 1);

        var response = Channel.SendRequest(request);

        if (response is ExceptionResponse exResponse)
            throw new CacheException("Error while switching read-only mode", exResponse.Message,
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

        var r = (GenerateUniqueIdsResponse)response;

        return r.Ids;
    }

    public void Put(string collectionName, PackedObject item, bool excludeFromEviction = false)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));


        var request = new PutRequest(collectionName) { ExcludeFromEviction = excludeFromEviction };

        // by default this property is set to the schema name

        request.Items.Add(item);

        var response = Channel.SendRequest(request);

        if (response is ExceptionResponse exResponse)
            throw new CacheException("Error while writing an object", exResponse.Message,
                exResponse.CallStack);
    }

    public void FeedMany(string collectionName, IEnumerable<PackedObject> items, bool excludeFromEviction,
                         int packetSize = 50000)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));


        var sessionId = Guid.NewGuid();


        var enumerator = items.GetEnumerator();

        var endLoop = false;

        while (!endLoop)
        {
            var packet = new PackedObject[packetSize];


            for (var i = 0; i < packetSize; i++)
                if (enumerator.MoveNext())
                {
                    var item = enumerator.Current;
                    packet[i] = item;
                }
                else
                {
                    endLoop = true;
                    enumerator.Dispose();
                    break;
                }


            if (collectionName == null) continue;

            var request = new PutRequest(collectionName)
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

    public int RemoveMany(OrQuery query, bool drop = false)
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));

        if (drop && !query.IsEmpty()) throw new ArgumentException("Invalid request. Drop table with a non empty query");

        var request = new RemoveManyRequest(query, drop);

        var response = Channel.SendRequest(request);

        if (response is ExceptionResponse exResponse)
            throw new CacheException("Error in RemoveMany", exResponse.Message, exResponse.CallStack);

        if (response is not ItemsCountResponse countResponse)
            throw new CacheException("Invalid type of response received in RemoveMany()");

        return countResponse.ItemsCount;
    }

    public PivotLevel ComputePivot(OrQuery filter, IEnumerable<int> axis, IEnumerable<int> values)
    {
        if (filter == null)
            throw new ArgumentNullException(nameof(filter));

        var request = new PivotRequest(filter);

        request.AxisList.AddRange(axis);
        request.ValuesList.AddRange(values);

        var response = Channel.SendRequest(request);

        if (response is ExceptionResponse exResponse)
            throw new CacheException("Error in ComputePivot", exResponse.Message, exResponse.CallStack);

        if (!(response is PivotResponse pivotResponse))
            throw new CacheException("Invalid type of response received in ComputePivot()");

        return pivotResponse.Root;
    }

    public int Truncate(string collectionName, bool drop = false)
    {
        // as we pass an empty query, it will be treated as a special request by the server
        return RemoveMany(new(collectionName), drop);
    }


    public IEnumerable<RankedItem> GetMany(OrQuery query, Guid sessionId = default)
    {
        Dbg.Trace($"one client GetMany for session {sessionId}");
        var request = new GetRequest(query, sessionId);

        return Channel.SendStreamRequest(request);
    }


    public bool Ping()
    {
        try
        {
            var description = GetClusterInformation();
            return description.ServersStatus.Length > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void DeclareCollection(string collectionName, CollectionSchema schema, int shard = -1)
    {
        // do not modify the original
        schema = schema.Clone();

        schema.CollectionName = collectionName;

        var request = new RegisterTypeRequest(schema, shard == -1 ? ShardIndex : shard, ShardsCount);

        var response = Channel.SendRequest(request);

        if (response is ExceptionResponse exResponse)
            throw new CacheException("Error while registering a type on the server", exResponse.Message,
                exResponse.CallStack);
    }

    public Tuple<bool, int> EvalQuery(OrQuery query)
    {
        var request = new EvalRequest(query);

        var response = Channel.SendRequest(request);

        if (response is ExceptionResponse exResponse)
            throw new CacheException("Error while getting server information", exResponse.Message,
                exResponse.CallStack);

        var concreteResponse = (EvalResponse)response;
        return new(concreteResponse.Complete, concreteResponse.Items);
    }

    public void Dump(string path)
    {
        var request = new DumpRequest { Path = path, ShardIndex = ShardIndex };
        var response = Channel.SendRequest(request);

        if (response is ExceptionResponse exResponse)
            throw new CacheException("Error while dumping all data", exResponse.Message,
                exResponse.CallStack);
    }

    public void ImportDump(string path)
    {
        var nodesFromPath = path.Split('_').FirstOrDefault(x => x.Contains("nodes"));
        if (nodesFromPath != null)
        {
            var nodes = int.Parse(nodesFromPath[..2]);
            if (nodes != 1)
                throw new CacheException(
                    $"You can not restore a {nodes} nodes dump to a single node cluster. Use DROP + FEED instead ");
        }

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

        foreach (var typeDescription in schema.CollectionsDescriptions)
        {
            // register the type on the server

            var request = new RegisterTypeRequest(typeDescription.Value);

            var response = Channel.SendRequest(request);

            if (response is ExceptionResponse exResponse)
                throw new CacheException("Error while registering a type on the server", exResponse.Message,
                    exResponse.CallStack);

            FeedMany(typeDescription.Value.CollectionName, DumpHelper.ObjectsInDump(path, typeDescription.Value), true);
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
                if ((keyFound && maxValues[pair.Key] < pair.Value) || !keyFound) maxValues[pair.Key] = pair.Value;
            }
        }

        // resync sequences on the server

        ResyncUniqueIds(maxValues);
    }

    public void Stop(bool restart)
    {
        var request = new StopRequest(restart);

        Channel.SendRequest(request);
    }

    public void ExecuteTransaction(IList<DataRequest> requests)
    {
        var request = new TransactionRequest(requests)
        { IsSingleStage = true, TransactionId = Guid.NewGuid() };

        TransactionStatistics.ExecutedAsSingleStage();


        var response = Channel.SendRequest(request);

        switch (response)
        {
            case NullResponse:
                return;
            case ExceptionResponse exResponse when exResponse.ExceptionType != ExceptionType.FailedToAcquireLock:
                throw new CacheException(exResponse.Message, exResponse.ExceptionType);
            default:
                TransactionStatistics.NewTransactionCompleted();
                break;
        }
    }

    public void Import(string collectionName, string jsonFile)
    {
        var objects = DumpHelper.LoadObjects(this, jsonFile, collectionName);

        FeedMany(collectionName, objects, true);
    }

    public bool TryAdd(string collectionName, PackedObject item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));


        var request = new PutRequest(collectionName) { ExcludeFromEviction = true, OnlyIfNew = true };

        request.Items.Add(item);

        var response = Channel.SendRequest(request);

        if (response is ExceptionResponse exResponse)
            throw new CacheException("Error while writing an object to the cache", exResponse.Message,
                exResponse.CallStack);

        if (response is ItemsCountResponse count) return count.ItemsCount > 0;

        throw new NotSupportedException($"Unknown answer type received in TryAdd:{response.GetType()}");
    }

    public void UpdateIf(PackedObject newValue, OrQuery testAsQuery)
    {
        if (newValue == null)
            throw new ArgumentNullException(nameof(newValue));


        var request = new PutRequest(testAsQuery.CollectionName)
        { ExcludeFromEviction = true, Predicate = testAsQuery };

        request.Items.Add(newValue);

        var response = Channel.SendRequest(request);

        if (response is ExceptionResponse exResponse)
            throw new CacheException("Error while writing an object to the cache", exResponse.Message,
                exResponse.CallStack);
    }


    /// <summary>
    ///     Acquire lock on a single node cluster
    /// </summary>
    /// <param name="writeAccess"></param>
    /// <param name="collections"></param>
    /// <returns></returns>
    public Guid AcquireLock(bool writeAccess, params string[] collections)
    {
        if (collections.Length == 0)
            throw new ArgumentException("Value cannot be an empty collection.", nameof(collections));

        var sessionId = Guid.NewGuid();

        Channel.ReserveConnection(sessionId);

        LockPolicy.SmartRetry(() => TryAcquireLock(sessionId, writeAccess, collections));

        return sessionId;
    }

    public void ReleaseLock(Guid sessionId)
    {
        Dbg.Trace($"one client release lock for session {sessionId}");

        if (sessionId == Guid.Empty)
            throw new ArgumentException("Invalid sessionId in ReleaseLock");

        var request = new LockRequest
        {
            SessionId = sessionId,
            Unlock = true
        };

        var response = Channel.SendRequest(request);

        if (response is LockResponse) return;

        if (response is ExceptionResponse exResponse)
            throw new CacheException("Error while writing an object to the cache", exResponse.Message,
                exResponse.CallStack);

        throw new CacheException("Unexpected response type for AcquireLock");
    }

    public void DeclareDataFullyLoaded(string collectionName, bool isFullyLoaded)
    {
        DeclareDomain(new(OrQuery.Empty(collectionName), isFullyLoaded));
    }

    public bool IsDataFullyLoaded(string collectionName)
    {
        var serverDescription = GetServerDescription();

        // ReSharper disable AssignNullToNotNullAttribute
        if (serverDescription.DataStoreInfoByFullName.TryGetValue(collectionName, out var info))
            // ReSharper restore AssignNullToNotNullAttribute
            return info.AvailableData.IsFullyLoaded;

        return false;
    }

    public void DeclareDomain(DomainDescription domain)
    {
        if (domain == null)
            throw new ArgumentNullException(nameof(domain));

        if (domain.DescriptionAsQuery.CollectionName == null)
            throw new ArgumentNullException(nameof(domain), "CollectionName not specified");

        var request = new DomainDeclarationRequest(domain);
        var response = Channel.SendRequest(request);

        if (response is ExceptionResponse exResponse)
            throw new CacheException("Error while declaring a domain", exResponse.Message, exResponse.CallStack);
    }

    public void ConfigEviction(string collectionName, EvictionType evictionType, int limit, int itemsToRemove,
                               int timeLimitInMilliseconds)
    {
        if (evictionType == EvictionType.LessRecentlyUsed && timeLimitInMilliseconds != 0)
            throw new ArgumentException($"{nameof(timeLimitInMilliseconds)} can be used only for LRU eviction");

        if (evictionType == EvictionType.TimeToLive && (limit != 0 || itemsToRemove != 0))
            throw new ArgumentException(
                $"{nameof(limit)} and {nameof(itemsToRemove)} can be used only for LRU eviction");

        var request =
            new EvictionSetupRequest(collectionName, evictionType, limit, itemsToRemove, timeLimitInMilliseconds);

        var response = Channel.SendRequest(request);

        if (response is ExceptionResponse exResponse)
            throw new CacheException("Error while declaring a domain", exResponse.Message, exResponse.CallStack);
    }

    public void ReleaseConnections(Guid sessionId)
    {
        Channel.ReleaseConnection(sessionId);
    }

    /// <summary>
    ///     First stage of dump files import. The server is switched to admin mode and data files are moved to allow rollback
    /// </summary>
    /// <param name="path"></param>
    internal void ImportDumpStage0(string path)
    {
        var request = new ImportDumpRequest { Path = path, ShardIndex = ShardIndex, Stage = 0 };

        var response = Channel.SendRequest(request);

        if (response is ExceptionResponse exceptionResponse)
            throw new CacheException("Error while importing dump", exceptionResponse.Message,
                exceptionResponse.CallStack);
    }

    /// <summary>
    ///     Import data from dump files
    /// </summary>
    /// <param name="path"></param>
    internal void ImportDumpStage1(string path)
    {
        var request = new ImportDumpRequest { Path = path, Stage = 1, ShardIndex = ShardIndex };

        var response = Channel.SendRequest(request);

        if (response is ExceptionResponse exceptionResponse)
            throw new CacheException("Error while importing dump.", exceptionResponse.Message,
                exceptionResponse.CallStack);
    }

    /// <summary>
    ///     Last stage of successful dump import. Delete backup files and disable the admin mode
    /// </summary>
    /// <param name="path"></param>
    internal void ImportDumpStage2(string path)
    {
        var request = new ImportDumpRequest { Path = path, Stage = 2, ShardIndex = ShardIndex };

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
    internal void ImportDumpStage3(string path)
    {
        var request = new ImportDumpRequest { Path = path, Stage = 3, ShardIndex = ShardIndex };

        var response = Channel.SendRequest(request);

        if (response is ExceptionResponse exceptionResponse)
            throw new CacheException("Error during rollback", exceptionResponse.Message,
                exceptionResponse.CallStack);
    }

    internal void ResyncUniqueIds(IDictionary<string, int> newValues)
    {
        var request = new ResyncUniqueIdsRequest(new(newValues), ShardIndex, ShardsCount);

        var response = Channel.SendRequest(request);

        if (response is ExceptionResponse exResponse)
            throw new CacheException("Error while resyncing unique id generators", exResponse.Message,
                exResponse.CallStack);
    }

    internal bool TryAcquireLock(Guid sessionId, bool writeAccess, params string[] collections)
    {
        if (collections.Length == 0)
            throw new ArgumentException("Value cannot be an empty collection.", nameof(collections));


        var request = new LockRequest
        {
            SessionId = sessionId,
            WaitDelayInMilliseconds = 20,
            WriteMode = writeAccess,
            CollectionsToLock = new List<string>(collections)
        };

        var response = Channel.SendRequest(request);


        if (response is LockResponse lockResponse) return lockResponse.Success;

        if (response is ExceptionResponse exResponse)
            throw new CacheException("Error while trying to acquire lock", exResponse.Message,
                exResponse.CallStack);

        throw new CacheException("Unexpected response type for AcquireLock");
    }

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

    public void ReserveConnection(Guid sessionId)
    {
        Channel.ReserveConnection(sessionId);
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