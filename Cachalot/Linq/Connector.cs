using System;
using System.Collections.Generic;
using System.Threading;
using Channel;
using Client.Core;
using Client.Interface;
using JetBrains.Annotations;
using Server;

// ReSharper disable AssignNullToNotNullAttribute

namespace Cachalot.Linq
{
    public class Connector : IDisposable
    {
        readonly Dictionary<string, CollectionSchema> _collectionSchema = new Dictionary<string, CollectionSchema>();


        /// <summary>
        /// Declare a collection with explicit schema
        /// </summary>
        /// <param name="collectionName"></param>
        /// <param name="schema"></param>
        public void DeclareCollection(string collectionName, CollectionSchema schema)
        {
            
            lock (_collectionSchema)
            {
                if (_collectionSchema.TryGetValue(collectionName, out var oldSchema))
                {
                    if (!schema.Equals(oldSchema))
                    {
                        throw new CacheException($"Schema declaration conflict for collection {collectionName}");
                    }
                }
                else
                {
                    _collectionSchema[collectionName] = schema;
                }

                // redeclare anyway in case the server is a non persistent cache and it has restarted since the last declaration
                Client.DeclareCollection(collectionName, schema);
            }
        }


        readonly ThreadLocal<Guid> _currentSession = new ThreadLocal<Guid>();

        public Guid CurrentSession => _currentSession.Value;

        /// <summary>
        /// Perform read-only operations in a consistent context.It guarantees that multiple operations on multiple collections, even on a multi-node clusters
        /// give a consistent result. No write operation (normal or transactional) will be executed while the context is open.
        /// 
        /// </summary>
        /// <param name="action"></param>
        /// <param name="collections"></param>
        public void DoInConsistentReadOnlyContext(Action action, [NotNull] params string[] collections)
        {
            if (collections == null) throw new ArgumentNullException(nameof(collections));
            if (collections.Length == 0)
                throw new ArgumentException("Value cannot be an empty collection.", nameof(collections));


            // check that the collections have been declared
            lock (_collectionSchema)
            {
                foreach (var collection in collections)
                    if (!_collectionSchema.ContainsKey(collection))
                        throw new NotSupportedException(
                            $"Unknown collection {collection}. Use Connector.DeclareCollection");
            }

            Guid sessionId = default;
            try
            {
                sessionId = Client.AcquireLock(false, collections);

                _currentSession.Value = sessionId;

                action();
            }
            finally
            {
                Client.ReleaseLock(sessionId);
                _currentSession.Value = default;// close the session
            }

        }
       

        /// <summary>
        /// Declare collection with implicit schema (inferred from the type)
        /// If the collection name is not specified, the full name of the type is used
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collectionName"></param>
        public void DeclareCollection<T>(string collectionName = null)
        {
            collectionName ??= typeof(T).FullName;

            var description = TypeDescriptionsCache.GetDescription(typeof(T));
            
            var schema = description;
            
            Client.DeclareCollection(collectionName, schema);

            lock (_collectionSchema)
            {
                _collectionSchema[collectionName] = schema;
            }
        }

        public CollectionSchema GetCollectionSchema(string collectionName)
        {
            lock (_collectionSchema)
            {
                if (_collectionSchema.TryGetValue(collectionName, out var schema))
                    return schema;

                return null;
            }
        }

        private Server.Server _server;


        public Connector(string connectionString):this(new ClientConfig(connectionString))
        {
        }

        public Connector(ClientConfig config)
        {
            if (Client == null)
            {
                if (config.Servers == null || config.Servers.Count == 0)
                {
                    var channel = new InProcessChannel();
                    Client = new DataClient{Channel = channel};

                    _server = new Server.Server(new NodeConfig {IsPersistent = config.IsPersistent})
                        {Channel = channel};

                    _server.Start();
                }
                else if (config.Servers.Count == 1)
                {
                    var serverCfg = config.Servers[0];

                    var channel = new TcpClientChannel(new TcpClientPool(4, 1, serverCfg.Host, serverCfg.Port));

                    Client = new DataClient {Channel = channel};
                }
                else // multiple servers
                {
                    var aggregator = new DataAggregator();

                    var index = 0;
                    foreach (var serverConfig in config.Servers)
                    {
                        var channel =
                            new TcpClientChannel(new TcpClientPool(4, 1, serverConfig.Host, serverConfig.Port));

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
            if (quantity == 0)
            {
                throw new CacheException("When generating unique ids quantity must be at least 1");
            }
            return Client.GenerateUniqueIds(generatorName, quantity);
        }

        public ClusterInformation GetClusterDescription()
        {
            return Client.GetClusterInformation();
        }

        public DataSource<T> DataSource<T>(string collectionName = null)
        {
            collectionName ??= typeof(T).FullName;

            lock (_collectionSchema)
            {
                if (_collectionSchema.TryGetValue(collectionName, out var schema))
                {
                    return new DataSource<T>(this, collectionName, schema);
                } 
            }

            throw new CacheException($"No schema available for collection {collectionName}. Use Connector.DeclareCollection");
        }

        public DataAdmin AdminInterface()
        {
            return new DataAdmin(Client);
        }
    }
}