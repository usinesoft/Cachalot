//#define DEBUG_VERBOSE

using Channel;
using Client;
using Client.Core;
using Client.Interface;
using Client.Parsing;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;

// ReSharper disable AssignNullToNotNullAttribute

namespace Cachalot.Linq
{
    public class Connector : IDisposable
    {
        private readonly Dictionary<string, CollectionSchema> _collectionSchema =
            new Dictionary<string, CollectionSchema>();


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


        #region consistent read

        //TODO check if it can work on the aggregator only
        private readonly SemaphoreSlim _consistentReadSync = new SemaphoreSlim(10, 10);


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

            Guid sessionId = default;
            try
            {
                _consistentReadSync.Wait();

                sessionId = Client.AcquireLock(false, collections);

                Dbg.Trace($"Entered consistent read for session {sessionId}");

                action(new ConsistentContext(sessionId, this, collections));
            }
            catch (Exception e)
            {
                Dbg.Trace($"exception in consistent read session {sessionId} : {e.Message}");
            }
            finally
            {
                Dbg.Trace($"exit consistent read session {sessionId}");

                if (sessionId != default)
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

                return null;
            }
        }

        private Server.Server _server;


        /// <summary>
        /// No parameters => we will create an internal, non persistent server
        /// </summary>
        public Connector(bool isPersistent = false):this(new ClientConfig(){IsPersistent = isPersistent})
        {

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

                _server = new Server.Server(new NodeConfig
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

                var channel = new TcpClientChannel(new TcpClientPool(config.ConnectionPoolCapacity,
                    config.PreloadedConnections, serverCfg.Host, serverCfg.Port));

                Client = new DataClient { Channel = channel };
            }
            else // multiple servers
            {
                var aggregator = new DataAggregator();

                var index = 0;
                foreach (var serverConfig in config.Servers)
                {
                    var channel =
                        new TcpClientChannel(new TcpClientPool(config.ConnectionPoolCapacity,
                            config.PreloadedConnections, serverConfig.Host, serverConfig.Port));

                    var client = new DataClient
                    {
                        Channel = channel,
                        ShardIndex = index,
                        ShardsCount = config.Servers.Count
                    };
                    aggregator.CacheClients.Add(client);
                    index++;
                }


                Client = aggregator;
            }
        }

        internal IDataClient Client { get; private set; }


        public void Dispose()
        {
            Client.Dispose();
            Client = null;

            if (_server != null)
            {
                _server.Stop();
                _server = null;
            }
        }

        /// <summary>
        ///     Initiate a write-only transaction
        /// </summary>
        /// <returns></returns>
        public Transaction BeginTransaction()
        {
            return new Transaction(this);
        }


        /// <summary>
        ///     Generate <paramref name="quantity" /> unique identifiers
        ///     They are guaranteed to be unique but they are not necessary in a contiguous range
        /// </summary>
        /// <param name="generatorName">name of the generator</param>
        /// <param name="quantity">number of unique ids to generate</param>
        public int[] GenerateUniqueIds(string generatorName, int quantity)
        {
            if (quantity == 0) throw new CacheException("When generating unique ids quantity must be at least 1");
            return Client.GenerateUniqueIds(generatorName, quantity);
        }

        public ClusterInformation GetClusterDescription()
        {
            return Client.GetClusterInformation();
        }


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

        public DataSource<T> DataSource<T>(string collectionName = null)
        {
            collectionName ??= typeof(T).Name;

            return new DataSource<T>(this, collectionName, GetCollectionSchema(collectionName));

        }


        internal IQueryable<T> ReadOnlyCollection<T>(Guid sessionId, string collectionName = null)
        {
            collectionName ??= typeof(T).Name;

            return new DataSource<T>(this, collectionName, GetCollectionSchema(collectionName), sessionId);

        }

        public DataAdmin AdminInterface()
        {
            return new DataAdmin(Client);
        }

        public IEnumerable<JObject> SqlQueryAsJson(string sql)
        {
            var parsed = new Parser().ParseSql(sql);

            var fromNode = parsed.Children.FirstOrDefault(n => n.Token == "from");

            var tableName = fromNode.Children.Single().Token;


            CollectionSchema schema = GetCollectionSchema(tableName);

            if (schema == null) 
            {
                var info = Client.GetClusterInformation();
                schema = info.Schema.Where(x=>x.CollectionName.ToUpper() == tableName.ToUpper()).FirstOrDefault();
                if(schema == null)
                {
                    throw new CacheException($"Unknown collection: {schema.CollectionName}");
                }

                lock (_collectionSchema)
                {
                    _collectionSchema[schema.CollectionName.ToUpper()] = schema;
                }
                
            }

            var query = parsed.ToQuery(schema);


            if (query.CountOnly)
            {
               (_, var count) = Client.EvalQuery(query);

                var result = new JObject();
                result["count"] = count;

                return new[] { result };
                
            }

            return Client.GetMany(query).Select(ri => ri.Item);
        }

        
        private IEnumerable<PackedObject> PackJson(IEnumerable<JObject> items, CollectionSchema schema, string collectionName = null)
        {
            foreach (var item in items)
            {
                yield return PackedObject.PackJson(item, schema, collectionName);
            }
        }

        /// <summary>
        /// Pack a data line of a csv file 
        /// </summary>
        /// <param name="lines"></param>
        /// <param name="schema"></param>
        /// <param name="collectionName"></param>
        /// <returns></returns>
        private IEnumerable<PackedObject> PackCsv(IEnumerable<string> lines, string collectionName, char separator = ',')
        {
            int primaryKey = 100; 

            foreach (var line in lines)
            {
                yield return PackedObject.PackCsv(primaryKey, line, collectionName, separator);

                primaryKey++;
            }
        }

        public void FeedWithJson(string collectionName, IEnumerable<JObject> items)
        {
            var schema = GetCollectionSchema(collectionName);

            Client.FeedMany(collectionName, PackJson(items, schema, collectionName), false);
        }

        /// <summary>
        /// Feed with CSV data lines (exclude header)
        /// </summary>
        /// <param name="collectionName"></param>
        /// <param name="items"></param>
        public void FeedWithCsvLines(string collectionName, IEnumerable<string> lines, char separator = ',')
        {
            
            Client.FeedMany(collectionName, PackCsv(lines, collectionName, separator), false);
        }
    }
}