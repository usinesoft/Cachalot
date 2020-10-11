using System;
using System.Collections.Generic;
using Channel;
using Client.Core;
using Client.Interface;
using Server;

// ReSharper disable AssignNullToNotNullAttribute

namespace Cachalot.Linq
{
    public class Connector : IDisposable
    {
        
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
                    Client = new CacheClient {Channel = channel};

                    _server = new Server.Server(new NodeConfig {IsPersistent = config.IsPersistent})
                        {Channel = channel};

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

                TypeDescriptionsCache.AddExplicitTypeDescription(type, typeDescription);
            }
        }

        private ICacheClient Client { get; set; }


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
            return new Transaction(Client);
        }



        /// <summary>
        ///     Generate <paramref name="quantity" /> unique identifiers
        ///     They are guaranteed to be unique but they are not necessary in a contiguous range
        /// </summary>
        /// <param name="generatorName">name of the generator</param>
        /// <param name="quantity">number of unique ids to generate</param>
        public int[] GenerateUniqueIds(string generatorName, int quantity)
        {
            return Client.GenerateUniqueIds(generatorName, quantity);
        }

        public ClusterInformation GetClusterDescription()
        {
            return Client.GetClusterInformation();
        }

        public DataSource<T> DataSource<T>()
        {
            ClientSideTypeDescription typeDescription = TypeDescriptionsCache.GetDescription(typeof(T));
            
            typeDescription = Client.RegisterType(typeof(T), typeDescription);
            
            return new DataSource<T>(Client, typeDescription);
        }

        public DataAdmin AdminInterface()
        {
            return new DataAdmin(Client);
        }
    }
}