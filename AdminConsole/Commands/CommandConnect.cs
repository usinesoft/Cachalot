using System;
using Channel;
using Client.Core;
using Client.Interface;

namespace AdminConsole.Commands
{
    public class CommandConnect : CommandBase
    {
        
        internal override ICacheClient TryExecute(ICacheClient client)
        {
            if (!CanExecute)
                return client;

            try
            {
                var server = "localhost";

                bool singleServerMode = !(Params.Count == 1 && Params[0].EndsWith(".xml"));

                if(!singleServerMode)
                {
                    server = Params[0];
                }

                var port = 4848;

                if (Params.Count > 1)
                {
                    port = int.Parse(Params[1]);
                }


                if (singleServerMode)
                {
                    var newClient = new CacheClient();
                    var channel = new TcpClientChannel(new TcpClientPool(1, 1, server, port));
                    newClient.Channel = channel;

                    if (newClient.Ping())
                    {
                        Logger.Write($"Connected to server {server} port {port}");

                        return newClient;
                    }

                
                
                }
                else // the unique  parameter is a cluster configuration file: connect to multiple servers
                {
                    var aggregator = new Aggregator();

                    var config = new ClientConfig();
                    config.LoadFromFile(Params[0]);

                    var index = 0;
                    foreach (var serverConfig in config.Servers)
                    {
                        var channel =
                            new TcpClientChannel(new TcpClientPool(4, 1, serverConfig.Host, serverConfig.Port));

                        var oneClient = new Client.Core.CacheClient
                        {
                            Channel = channel,
                            ShardIndex = index,
                            ShardsCount = config.Servers.Count
                        };
                        aggregator.CacheClients.Add(oneClient);
                        index++;
                    }

                    if (aggregator.Ping())
                    {
                        Logger.Write($"Connected to a cluster of {config.Servers.Count} servers");
                    }
                    

                    return aggregator;
                }
            }
            catch (Exception e)
            {
                Logger.WriteEror("Connection error:" + e.Message);
            }
            

            return client;
        }
    }
}