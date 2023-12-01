//#define DEBUG_VERBOSE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cachalot.Extensions;
using Channel;
using Client;
using Client.Core;
using Client.Interface;
using Client.Parsing;
using Client.Tools;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

// ReSharper disable AssignNullToNotNullAttribute

namespace Cachalot.Linq;

public sealed class Connector : IDisposable
{
    private readonly Dictionary<string, CollectionSchema> _collectionSchema = new();

    private int _lastUniqueIdForInternalServer = 1;

    private Server.Server _server;

    /// <summary>
    ///     No parameters => we will create an internal, non persistent server
    /// </summary>
    public Connector(bool isPersistent = false) : this(new ClientConfig { IsPersistent = isPersistent })
    {
        IsInternalServer = true;
        IsPersistent = isPersistent;
    }

    public Connector(string connectionString) : this(new ClientConfig(connectionString))
    {
    }

    public Connector([NotNull] ClientConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        if (config.Servers == null || config.Servers.Count == 0)
        {
            var channel = new InProcessChannel();
            Client = new DataClient { Channel = channel };

            _server = new(new()
            {
                IsPersistent = config.IsPersistent,
                DataPath = "."
            })
            { Channel = channel };

            _server.Start();
        }
        else if (config.Servers.Count == 1)
        {
            var serverCfg = config.Servers[0];

            var channel = new TcpClientChannel(new(config.ConnectionPoolCapacity,
                config.PreloadedConnections, serverCfg.Host, serverCfg.Port));

            Client = new DataClient
            {
                Channel = channel,
                ServerHostname = serverCfg.Host,
                ServerPort = serverCfg.Port
            };
        }
        else // multiple servers
        {
            var aggregator = new DataAggregator();

            var index = 0;
            foreach (var serverConfig in config.Servers)
            {
                var channel =
                    new TcpClientChannel(new(config.ConnectionPoolCapacity,
                        config.PreloadedConnections, serverConfig.Host, serverConfig.Port));

                var client = new DataClient
                {
                    Channel = channel,
                    ShardIndex = index,
                    ShardsCount = config.Servers.Count,
                    ServerHostname = serverConfig.Host,
                    ServerPort = serverConfig.Port
                };
                aggregator.CacheClients.Add(client);
                index++;
            }


            Client = aggregator;
        }
    }

    public bool IsInternalServer { get; }

    internal IDataClient Client { get; private set; }


    /// <summary>
    ///     Special collection containing the server-side activity log
    /// </summary>
    public IQueryable<LogEntry> ActivityLog
    {
        get
        {
            var schema = TypeDescriptionsCache.GetDescription(typeof(LogEntry));

            return new DataSource<LogEntry>(this, "@ACTIVITY", schema);
        }
    }

    public bool IsPersistent { get; }


    public void Dispose()
    {
        Client?.Dispose();
        Client = null;

        if (_server != null)
        {
            _server.Stop();
            _server = null;
        }
    }


    /// <summary>
    ///     Declare a collection with explicit schema
    /// </summary>
    /// <param name="collectionName"></param>
    /// <param name="schema"></param>
    public void DeclareCollection(string collectionName, CollectionSchema schema)
    {
        lock (_collectionSchema)
        {
            var key = collectionName.Trim().ToUpper();
            if (_collectionSchema.TryGetValue(key, out var oldSchema))
            {
                if (!schema.Equals(oldSchema))
                    throw new CacheException($"Schema declaration conflict for collection {collectionName}");
            }
            else
            {
                _collectionSchema[key] = schema;
            }

            // redeclare anyway in case the server is a non persistent cache and it has restarted since the last declaration
            Client.DeclareCollection(collectionName, schema);
        }
    }


    /// <summary>
    ///     Declare collection with implicit schema (inferred from the type)
    ///     If the collection name is not specified, the name of the type is used
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="collectionName"></param>
    public void DeclareCollection<T>(string collectionName = null)
    {
        collectionName ??= typeof(T).Name;

        var description = TypeDescriptionsCache.GetDescription(typeof(T));

        var schema = description;

        Client.DeclareCollection(collectionName, schema);

        lock (_collectionSchema)
        {
            _collectionSchema[collectionName.Trim().ToUpper()] = schema;
        }
    }

    public CollectionSchema GetCollectionSchema(string collectionName)
    {
        lock (_collectionSchema)
        {
            if (_collectionSchema.TryGetValue(collectionName.Trim().ToUpper(), out var schema))
                return schema;


            // try to get schema from server   
            var info = Client.GetClusterInformation();
            schema = Array.Find(info.Schema, x =>
                string.Equals(x.CollectionName, collectionName, StringComparison.CurrentCultureIgnoreCase));
            if (schema == null) return null;

            _collectionSchema[schema.CollectionName.ToUpper()] = schema;


            return schema;
        }
    }

    /// <summary>
    ///     Initiate a write-only transaction
    /// </summary>
    /// <returns></returns>
    public Transaction BeginTransaction()
    {
        return new(this);
    }

    /// <summary>
    ///     Generate <paramref name="quantity" /> unique identifiers
    ///     They are guaranteed to be unique but they are not necessary in a contiguous range
    /// </summary>
    /// <param name="generatorName">name of the generator</param>
    /// <param name="quantity">number of unique ids to generate</param>
    public int[] GenerateUniqueIds(string generatorName, int quantity)
    {
        // for non-persistent internal servers we can safely generate unique ids in client memory
        if (IsInternalServer && !IsPersistent)
        {
            var result = new int[quantity];
            for (var i = 0; i < quantity; i++) result[i] = Interlocked.Increment(ref _lastUniqueIdForInternalServer);

            return result;
        }

        if (quantity == 0) throw new CacheException("When generating unique ids quantity must be at least 1");
        return Client.GenerateUniqueIds(generatorName, quantity);
    }

    public ClusterInformation GetClusterDescription()
    {
        return Client.GetClusterInformation();
    }

    public DataSource<T> DataSource<T>(string collectionName = null)
    {
        collectionName ??= typeof(T).Name;

        return new(this, collectionName, GetCollectionSchema(collectionName));
    }

    public void DropCollection(string collectionName = null)
    {
        Client.RemoveMany(new(collectionName), true);

        lock (_collectionSchema)
        {
            var key = _collectionSchema.Keys.FirstOrDefault(k => k.Equals(collectionName, StringComparison.InvariantCultureIgnoreCase));
            if (key != null)
            {
                _collectionSchema.Remove(key);
            }
        }
    }


    internal IQueryable<T> ReadOnlyCollection<T>(Guid sessionId, string collectionName = null)
    {
        collectionName ??= typeof(T).Name;

        return new DataSource<T>(this, collectionName, GetCollectionSchema(collectionName), sessionId);
    }

    public DataAdmin AdminInterface()
    {
        return new(Client);
    }


    public int DeleteManyWithSQL(string sql)
    {
        sql = sql.Trim();

        // if a delete query is received rewrite it as select; the parser already exist
        if (sql.StartsWith("delete", StringComparison.InvariantCultureIgnoreCase))
        {
            sql = "select" + sql.Substring(6);
        }
        var parsed = new Parser().ParseSql(sql);

        var fromNode = parsed.Children.FirstOrDefault(n => n.Token == "from");

        if (fromNode == null) throw new CacheException($"Collection name missing in {sql}. FROM clause not found");

        var tableName = fromNode.Children.Single().Token;


        var schema = GetCollectionSchema(tableName);

        var query = parsed.ToQuery(schema);

        // ignore take clause for delete
        query.Take = 0;

        return Client.RemoveMany(query);
    }

    public IEnumerable<JObject> SqlQueryAsJson(string sql, string fullTextQuery = null, Guid queryId = default)
    {
        var parsed = new Parser().ParseSql(sql);

        var fromNode = parsed.Children.FirstOrDefault(n => n.Token == "from");

        if (fromNode == null) throw new CacheException($"Collection name missing in {sql}. FROM clause not found");

        var tableName = fromNode.Children.Single().Token;


        var schema = GetCollectionSchema(tableName);


        var query = parsed.ToQuery(schema);

        query.FullTextSearch = fullTextQuery;

        query.QueryId = queryId;


        if (query.CountOnly)
        {
            var (_, count) = Client.EvalQuery(query);

            var result = new JObject
            {
                ["count"] = count
            };

            return new[] { result };
        }

        return Client.GetMany(query).Select(ri => ri.Item);
    }

    public event EventHandler<ProgressEventArgs> Progress;


    private IEnumerable<PackedObject> PackJson(IEnumerable<JObject> items, CollectionSchema schema,
                                               string collectionName = null)
    {
        Progress?.Invoke(this, new(ProgressEventArgs.ProgressNotification.Start, 0));

        var processed = 0;

        foreach (var item in items)
        {
            processed++;
            yield return PackedObject.PackJson(item, schema, collectionName);

            if (processed % 10_000 != 0)
                continue;
            Progress?.Invoke(this, new(ProgressEventArgs.ProgressNotification.Progress, processed));
        }

        Progress?.Invoke(this, new(ProgressEventArgs.ProgressNotification.End, processed));
    }

    public void FeedWithJson(string collectionName, IEnumerable<JObject> items)
    {
        var schema = GetCollectionSchema(collectionName);

        Client.FeedMany(collectionName, PackJson(items, schema, collectionName), false);
    }

    internal void NotifyProgress(int processedItems, int totalItems = 0)
    {
        Progress?.Invoke(this, new(ProgressEventArgs.ProgressNotification.Progress, processedItems, totalItems));
    }

    /// <summary>
    ///     Feed with CSV data lines (exclude header)
    /// </summary>
    /// <param name="collectionName">the collection that will store the dta</param>
    /// <param name="lines">data lines of a csv (skip header)</param>
    /// <param name="csvSchema">contains information about the csv structure</param>
    public void FeedWithCsvLines(string collectionName, IEnumerable<string> lines, CsvSchema csvSchema)
    {
        Client.FeedMany(collectionName, this.PackCsv(lines, collectionName, csvSchema), false);
    }


    public void Truncate(string collectionName)
    {
        Client.Truncate(collectionName);
    }


    #region consistent read


    private readonly SemaphoreSlim _consistentReadSync = new(10, 10);


    /// <summary>
    ///     Perform read-only operations in a consistent context.It guarantees that multiple operations on multiple
    ///     collections, even on a multi-node clusters
    ///     give a consistent result. No write operation (normal or transactional) will be executed while the context is open.
    /// </summary>
    /// <param name="action">Should contain a list of queries performed on a <see cref="ConsistentContext" /></param>
    /// <param name="collections"></param>
    public void ConsistentRead(Action<ConsistentContext> action, [NotNull] params string[] collections)
    {
        if (collections.Length == 0)
            throw new ArgumentException("Value cannot be an empty collection.", nameof(collections));


        // check that the collections have been declared
        lock (_collectionSchema)
        {
            foreach (var collection in collections)
                if (!_collectionSchema.ContainsKey(collection.Trim().ToUpper()))
                    throw new NotSupportedException(
                        $"Unknown collection {collection}. Use Connector.DeclareCollection");
        }

        Guid sessionId = Guid.Empty;
        try
        {
            _consistentReadSync.Wait();

            sessionId = Client.AcquireLock(false, collections);

            Dbg.Trace($"Entered consistent read for session {sessionId}");

            action(new(sessionId, this, collections));
        }
        catch (Exception e)
        {
            Dbg.Trace($"exception in consistent read session {sessionId} : {e.Message}");
        }
        finally
        {
            Dbg.Trace($"exit consistent read session {sessionId}");

            if (sessionId != Guid.Empty)
            {
                Client.ReleaseLock(sessionId);
                Client.ReleaseConnections(sessionId);
            }

            _consistentReadSync.Release();
        }
    }

    /// <summary>
    ///     Helper for one collection with default naming
    /// </summary>
    /// <typeparam name="T1"></typeparam>
    /// <param name="action"></param>
    public void ConsistentRead<T1>(Action<ConsistentContext> action)
    {
        ConsistentRead(action, typeof(T1).Name);
    }

    /// <summary>
    ///     Helper for two collections with default naming
    /// </summary>
    /// <typeparam name="T1"></typeparam>
    /// <typeparam name="T2"></typeparam>
    /// <param name="action"></param>
    public void ConsistentRead<T1, T2>(Action<ConsistentContext> action)
    {
        ConsistentRead(action, typeof(T1).Name, typeof(T2).Name);
    }


    /// <summary>
    ///     Helper for three collections with default naming
    /// </summary>
    /// <typeparam name="T1"></typeparam>
    /// <typeparam name="T2"></typeparam>
    /// <typeparam name="T3"></typeparam>
    /// <param name="action"></param>
    public void ConsistentRead<T1, T2, T3>(Action<ConsistentContext> action)
    {
        ConsistentRead(action, typeof(T1).Name, typeof(T2).Name, typeof(T3).Name);
    }

    /// <summary>
    ///     Helper for four collections with default naming
    /// </summary>
    /// <typeparam name="T1"></typeparam>
    /// <typeparam name="T2"></typeparam>
    /// <typeparam name="T3"></typeparam>
    /// <typeparam name="T4"></typeparam>
    /// <param name="action"></param>
    public void ConsistentRead<T1, T2, T3, T4>(Action<ConsistentContext> action)
    {
        ConsistentRead(action, typeof(T1).Name, typeof(T2).Name, typeof(T3).Name, typeof(T4).Name);
    }

    #endregion
}