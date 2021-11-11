using System;
using Channel;
using Client.Core;
using Client.Interface;

namespace AdminConsole.Commands
{
    public class CommandConnect : CommandBase
    {
        internal override IDataClient TryExecute(IDataClient client)
        {
            if (!CanExecute)
                return client;

            try
            {
                // server or cluster my be specified as 
                // hostname                 => single node mode port defaults to 48401
                // hostname port            => single node mode
                // hostname: port           => single node mode
                // config.xml               => single node or cluster specified as configuration file
                // host1:post1+host2:port2  => cluster specified as connection string

                // simplified connection mostly for testing 1=localhost:48401 2=localhost:48401+localhost:48402
                if (Params[0].Trim() == "1")
                {
                    Params[0] = "localhost:48401";
                }

                if (Params[0].Trim() == "2")
                {
                    Params[0] = "localhost:48401+localhost:48402";
                }


                var server = "localhost";

                var port = Constants.DefaultPort;

                var isSimple = !Params[0].Contains(":"); // server port not a real connection string

                var parts = Params[0].Split();

                if (parts.Length > 0) server = parts[0];
                if (parts.Length > 1) port = int.Parse(parts[1]);


                if (isSimple)
                {
                    var newClient = new DataClient();
                    var channel = new TcpClientChannel(new TcpClientPool(1, 1, server, port));
                    newClient.Channel = channel;

                    if (newClient.Ping())
                    {
                        Logger.Write($"Connected to server {server} port {port}");

                        return newClient;
                    }
                }
                else // the unique  parameter is a cluster configuration file or a connection string
                {
                    var aggregator = new DataAggregator();

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

                        var oneClient = new DataClient
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