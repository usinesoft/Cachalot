using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Channel;
using Client;
using Client.Interface;
using NUnit.Framework;
using Server;
using Server.HostServices.Logger;

namespace Tests.IntegrationTests
{
    public class MultiServerTestFixtureBase
    {
        protected ClientConfig _clientConfig;

        private List<FastLogger> _loggers = new List<FastLogger>();

        protected List<ServerInfo> _servers = new List<ServerInfo>();

        protected int ServerCount = 10;


        protected void StopServers()
        {
            foreach (var serverInfo in _servers)
            {
                serverInfo.Channel.Stop();
                serverInfo.Server.Stop();
            }

            foreach (var logger in _loggers) logger.Stop();
        }

        protected void StartServers(int serverCount = 0)
        {
            _clientConfig = new ClientConfig();
            _servers = new List<ServerInfo>();
            _loggers = new List<FastLogger>();

            serverCount = serverCount == 0 ? ServerCount : serverCount;

            for (var i = 0; i < serverCount; i++)
            {
                var path = $"server{i:D2}";
                var serverInfo = new ServerInfo { Channel = new TcpServerChannel() };
                var nodeConfig = new NodeConfig { IsPersistent = true, DataPath = path };

                var logger = new FastLogger();
                logger.Start(path);
                _loggers.Add(logger);

                serverInfo.Server = new Server.Server(nodeConfig, logger)
                {
                    Channel = serverInfo.Channel
                };

                serverInfo.Port = serverInfo.Channel.Init();
                serverInfo.Channel.Start();
                serverInfo.Server.Start();

                _servers.Add(serverInfo);

                _clientConfig.Servers.Add(
                    new ServerConfig { Host = "localhost", Port = serverInfo.Port });
            }


            Thread.Sleep(500); //be sure the server nodes are started
        }

        protected void RestartOneServer()
        {
            var serverInfo = _servers[0];

            serverInfo.Channel.Stop();
            serverInfo.Server.Stop();

            // restart on the same port
            serverInfo.Port = serverInfo.Channel.Init(serverInfo.Port);
            serverInfo.Channel.Start();
            serverInfo.Server.Start();

            Thread.Sleep(500);
        }

        protected void TearDown()
        {
            StopServers();

            // deactivate all failure simulations
            Dbg.DeactivateSimulation();
        }

        protected void SetUp()
        {
            for (var i = 0; i < ServerCount; i++)
                if (Directory.Exists($"server{i:D2}"))
                    Directory.Delete($"server{i:D2}", true);


            StartServers();
        }

        protected static void OneTimeSetUp()
        {
            Environment.CurrentDirectory = TestContext.CurrentContext.TestDirectory;
            Directory.SetCurrentDirectory(TestContext.CurrentContext.TestDirectory);
        }

        protected class ServerInfo
        {
            public TcpServerChannel Channel { get; set; }
            public Server.Server Server { get; set; }
            public int Port { get; set; }
        }
    }
}