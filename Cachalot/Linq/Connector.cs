using System;
using System.Collections.Generic;
using Channel;
using Client.Core;
using Client.Interface;
using ServerConfig = Server.ServerConfig;

// ReSharper disable AssignNullToNotNullAttribute

namespace Cachalot.Linq
{
    public class Connector : IDisposable
    {
        public Transaction BeginTransaction()
        {
            return new Transaction(_typeDescriptions, Client);
        }


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

        private Server.Server _server;

        private readonly Dictionary<string, ClientSideTypeDescription> _typeDescriptions =
            new Dictionary<string, ClientSideTypeDescription>();


        /// <summary>
        ///     Register a type for which the keys are specified with attributes and not in the configuration file
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private ClientSideTypeDescription RegisterDynamicType(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            var description = Client.RegisterTypeIfNeeded(type);

            if (type.FullName != null) _typeDescriptions[type.FullName] = description;

            return description;
        }


        /// <summary>
        ///     Generate <paramref name="quantity" /> unique identifiers
        ///     They are guaranteed to be unique but they are not necesary in a contiguous range
        /// </summary>
        /// <param name="generatorName">name of the generator</param>
        /// <param name="quantity">number of unique ids to generate</param>
        public int[] GenerateUniqueIds(string generatorName, int quantity)
        {
            return Client.GenerateUniqueIds(generatorName, quantity);
        }

        public Connector(ClientConfig config)
        {
            if (Client == null)
            {
                if (config.Servers == null || config.Servers.Count == 0)
                {
                    var channel = new InProcessChannel();
                    Client = new CacheClient {Channel = channel};

                    _server = new Server.Server(new ServerConfig(), config.IsPersistent) {Channel = channel};

                    _server.Start();
                }
                else if (config.Servers.Count == 1)
                {
                    var serverCfg = config.Servers[0];

                    var channel = new TcpClientChannel(new TcpClientPool(4, 1, serverCfg.Host, serverCfg.Port));

                    Client = new CacheClient {Channel = channel};
                }
                else // multiple servers
                {
                    var aggregator = new Aggregator();

                    var index = 0;
                    foreach (var serverConfig in config.Servers)
                    {
                        var channel =
                            new TcpClientChannel(new TcpClientPool(4, 1, serverConfig.Host, serverConfig.Port));

                        var client = new CacheClient
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


            // register types from client configuration
            foreach (var description in config.TypeDescriptions)
            {
                var type = Type.GetType(description.Value.FullTypeName + ", " + description.Value.AssemblyName);
                var typeDescription = Client.RegisterTypeIfNeeded(type, description.Value);

                _typeDescriptions.Add(typeDescription.FullTypeName, typeDescription);
            }
        }

        public ClusterInformation GetClusterDescription()
        {
            return Client.GetClusterInformation();
        }

        private ICacheClient Client { get; set; }

        public DataSource<T> DataSource<T>()
        {
            var name = typeof(T).FullName;

            if (!_typeDescriptions.TryGetValue(name, out var typeDescription))
                typeDescription = RegisterDynamicType(typeof(T));

            return new DataSource<T>(Client, typeDescription);
        }

        public DataAdmin AdminInterface()
        {
            return new DataAdmin(Client);
        }
    }
}