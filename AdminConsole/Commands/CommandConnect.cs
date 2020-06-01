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
                // server or cluster my be specified as 
                // hostname                 => single node mode port defaults to 4848
                // hostname port            => single node mode
                // hostname: port           => single node mode
                // config.xml               => single node or cluster specified as configuration file
                // host1:post1+host2:port2  => cluster specified as connection string

                var server = "localhost";

                var singleServerMode = !(Params.Count == 1 && (Params[0].EndsWith(".xml") || Params[0].Contains('+')));

                if (!singleServerMode) server = Params[0];

                var port = 4848;

                if (Params.Count > 1) port = int.Parse(Params[1]);


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

                    ClientConfig config;

                    if (Params[0].EndsWith(".xml"))
                    {
                        config = new ClientConfig();
                        config.LoadFromFile(Params[0]);
                    }
                    else // a connection string
                    {
                        config = new ClientConfig(Params[0]);
                    }
                    

                    var index = 0;
                    foreach (var serverConfig in config.Servers)
                    {
                        var channel =
                            new TcpClientChannel(new TcpClientPool(4, 1, serverConfig.Host, serverConfig.Port));

                        var oneClient = new CacheClient
                        {
                            Channel = channel,
                            ShardIndex = index,
                            ShardsCount = config.Servers.Count
                        };
                        aggregator.CacheClients.Add(oneClient);
                        index++;
                    }

                    if (aggregator.Ping()) Logger.Write($"Connected to a cluster of {config.Servers.Count} servers");


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