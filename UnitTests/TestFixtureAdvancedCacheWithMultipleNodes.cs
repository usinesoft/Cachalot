using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cachalot.Linq;
using Channel;
using Client;
using Client.Interface;
using NUnit.Framework;
using UnitTests.TestData.Instruments;
using UnitTests.TestData.Events;
using ServerConfig = Server.ServerConfig;

namespace UnitTests
{
    [TestFixture]
    public class TestFixtureAdvancedCacheWithMultipleNodes
    {
        private class ServerInfo
        {
            public TcpServerChannel Channel { get; set; }
            public Server.Server Server { get; set; }
            public int Port { get; set; }
        }

        private  List<ServerInfo> _servers = new List<ServerInfo>();

        private const int ServerCount = 10;


        private void StopServers()
        {
            foreach (var serverInfo in _servers)
            {
                serverInfo.Channel.Stop();
                serverInfo.Server.Stop();
            }
        }

        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            Environment.CurrentDirectory = TestContext.CurrentContext.TestDirectory;
            Directory.SetCurrentDirectory(TestContext.CurrentContext.TestDirectory);
        }

        [TearDown]
        public void Exit()
        {
            StopServers();

            // deactivate all failure simulations
            Dbg.DeactivateSimulation();
        }

        [SetUp]
        public void Init()
        {
            
            StartServers();
        }
        private ClientConfig _clientConfig;

        private void StartServers(int serverCount = 0)
        {
            _clientConfig = new ClientConfig();
            _servers = new List<ServerInfo>();

            serverCount = serverCount == 0 ? ServerCount : serverCount;

            for (var i = 0; i < serverCount; i++)
            {
                var serverInfo = new ServerInfo { Channel = new TcpServerChannel() };
                serverInfo.Server = new Server.Server(new ServerConfig()) { Channel = serverInfo.Channel }; // start non-persistent server
                serverInfo.Port = serverInfo.Channel.Init(); // get the dynamically allocated ports
                serverInfo.Channel.Start();
                serverInfo.Server.Start();

                _servers.Add(serverInfo);

                _clientConfig.Servers.Add(
                    new Client.Interface.ServerConfig { Host = "localhost", Port = serverInfo.Port });
            }


            Thread.Sleep(500); //be sure the server nodes are started
        }



        [Test]
        public void Feed_cache_and_get_data()
        {
            using (var connector = new Connector(_clientConfig))
            {
                var dataSource = connector.DataSource<ProductEvent>();

                var events = new List<ProductEvent>();
                for (var i = 0; i < 100; i++)
                    switch (i % 3)
                    {
                        case 0:
                            events.Add(new FixingEvent(i, "AXA", 150, "EQ-256"));
                            break;
                        case 1:
                            events.Add(new FixingEvent(i, "TOTAL", 180, "IRD-400"));
                            break;
                        case 2:
                            events.Add(new Increase(i, 180, "EQ-256"));
                            break;
                    }
                dataSource.PutMany(events);

                var fixings = dataSource.Count(e => e.EventType == "FIXING");
                Assert.IsTrue(fixings > 50, "fixings > 50");


                var list = dataSource.Where(e => e.EventType == "FIXING").Take(10).ToList();
                Assert.AreEqual(10, list.Count);

            }
            
        }

        [Test]
        public void Feed_cache_and_declare_domain_completion()
        {
            using (var connector = new Connector(_clientConfig))
            {
                var dataSource = connector.DataSource<ProductEvent>();

                var events = new List<ProductEvent>();
                for (var i = 0; i < 100; i++)
                    switch (i % 3)
                    {
                        case 0:
                            events.Add(new FixingEvent(i, "AXA", 150, "EQ-256"));
                            break;
                        case 1:
                            events.Add(new FixingEvent(i, "TOTAL", 180, "IRD-400"));
                            break;
                        case 2:
                            events.Add(new Increase(i, 180, "EQ-256"));
                            break;
                    }
                dataSource.PutMany(events);

                Assert.Throws<CacheException>(() =>
                    // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                    dataSource.Where(e => e.EventType == "FIXING").OnlyIfComplete().ToList()
                    );
                


                // declare that all data is available
                dataSource.DeclareFullyLoaded();

                var fixings = dataSource.Where(e => e.EventType == "FIXING").OnlyIfComplete().ToList();
                Assert.Greater(fixings.Count, 0);


                // declare that data is not available again
                dataSource.DeclareFullyLoaded(false);

                Assert.Throws<CacheException>(() =>
                    // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                    dataSource.Where(e => e.EventType == "FIXING").OnlyIfComplete().ToList()
                );


                // declare that all fixings are available
                dataSource.DeclareLoadedDomain(p=>p.EventType == "FIXING");
                fixings = dataSource.Where(e => e.EventType == "FIXING" && e.EventDate == DateTime.Today).OnlyIfComplete().ToList();
                Assert.Greater(fixings.Count, 0);

                // the next query can not be guaranteed to be complete


                Assert.Throws<CacheException>(() =>
                    // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                    dataSource.Where(e => e.EventDate == DateTime.Today).OnlyIfComplete().ToList()
                );
            }
                
        }


        [Test]
        public void Test_lru_eviction()
        {
            using (var connector = new Connector(_clientConfig))
            {
                var trades = connector.DataSource<Trade>();
                trades.ConfigEviction(EvictionType.LessRecentlyUsed, 50, 5);


                // check that eviction is triggered by individual PUT operations
                for (int i = 0; i < 100; i++)
                {
                    var trade = new Trade{Id = i, ContractId = $"TRD-{i}", Counterparty = "YOU", IsLastVersion = true, Portfolio = "PTF44"};
                    trades.Put(trade);
                }

                var count = trades.Count();
                Assert.LessOrEqual(count, 50);
                
                // check that eviction is triggered by PUT MANY operations
                var list = new List<Trade>();
                for (int i = 100; i < 200; i++)
                {
                    var trade = new Trade { Id = i, ContractId = $"TRD-{i}", Counterparty = "YOU", IsLastVersion = true, Portfolio = "PTF44" };
                    list.Add(trade);
                    
                }

                trades.PutMany(list);

                count = trades.Count();
                Assert.LessOrEqual(count, 50);

            }
        }

    }
}