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
using Server;
using UnitTests.TestData;
using UnitTests.TestData.Events;
using Trade = UnitTests.TestData.Instruments.Trade;

namespace UnitTests
{
    public class TradeProvider
    {
        private Connector _connector;

        
        public void Startup(ClientConfig config)
        {
            _connector = new Connector(config);

            var trades = _connector.DataSource<Trade>();

            // remove 500 items every time the limit of 500_000 is reached
            trades.ConfigEviction(EvictionType.LessRecentlyUsed, 500_000, 500);
        }


        public Trade GetTrade(int id)
        {
            var trades = _connector.DataSource<Trade>();
            var fromCache = trades[id];

            if (fromCache != null)
            {
                LastOneWasFromCache = true;
                return fromCache;
            }

            var trade = GetTradeFromDatabase(id);
            trades.Put(trade);

            LastOneWasFromCache = false;
            return trade;
        }


        public void Shutdown()
        {
            _connector.Dispose();
        }


        private Trade GetTradeFromDatabase(int id)
        {
            return new Trade{Id = id, ContractId = $"TRD-{id}"};
        }


        public bool LastOneWasFromCache { get; set; }


    }



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
                serverInfo.Server = new Server.Server(new NodeConfig()) { Channel = serverInfo.Channel }; // start non-persistent server
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
        public void Domain_declaration_example()
        {
            using (var connector = new Connector(_clientConfig))
            {
                var homes = connector.DataSource<Home>();

                homes.Put(new Home{ Id = 1, CountryCode = "FR", PriceInEuros = 150, Town = "Paris", Rooms = 3, Bathrooms = 1});
                homes.Put(new Home{ Id = 2, CountryCode = "FR", PriceInEuros = 250, Town = "Paris", Rooms = 5, Bathrooms = 2});
                homes.Put(new Home{ Id = 3, CountryCode = "FR", PriceInEuros = 100, Town = "Nice", Rooms = 1, Bathrooms = 1});
                homes.Put(new Home{ Id = 4, CountryCode = "FR", PriceInEuros = 150, Town = "Nice", Rooms = 2, Bathrooms = 1});

                homes.DeclareLoadedDomain(h=>h.Town == "Paris" || h.Town == "Nice");

                // this one can be served from cache
                var result = homes.Where(h => h.Town == "Paris" && h.Rooms >= 2).OnlyIfComplete().ToList();

                Assert.AreEqual(2, result.Count);

                // this one too
                result = homes.Where(h => h.Town == "Nice" && h.Rooms == 2).OnlyIfComplete().ToList();

                Assert.AreEqual(1, result.Count);

                // this one thrown an exception as the query is not a subset of the domain
                Assert.Throws<CacheException>(() =>                    
                        result = homes.Where(h => h.CountryCode == "FR" && h.Rooms == 2).OnlyIfComplete().ToList()
                    );


                var trades = connector.DataSource<Trade>();

                trades.Put(new Trade{Id = 1, ContractId = "SWAP-001", Counterparty = "BNP", TradeDate = DateTime.Today, MaturityDate = DateTime.Today});
                trades.Put(new Trade{Id = 2, ContractId = "SWAP-002", Counterparty = "GOLDMAN", TradeDate = DateTime.Today.AddDays(-1), MaturityDate = DateTime.Today.AddDays(100) });
                trades.Put(new Trade{Id = 3, ContractId = "SWAP-003", Counterparty = "BNP", TradeDate = DateTime.Today.AddDays(-2), MaturityDate = DateTime.Today.AddDays(50), IsDestroyed = true});
                trades.Put(new Trade{Id = 4, ContractId = "SWAP-004", Counterparty = "MLINCH", TradeDate = DateTime.Today.AddDays(-3), MaturityDate = DateTime.Today.AddDays(15) });


                var oneYearAgo = DateTime.Today.AddYears(-1);
                var today = DateTime.Today;

                trades.DeclareLoadedDomain(t=>t.MaturityDate >= today || t.TradeDate > oneYearAgo);

                // this one can be served from cache
                var res =trades.Where(t=>t.IsDestroyed == false && t.TradeDate == DateTime.Today.AddDays(-1)).OnlyIfComplete().ToList();
                Assert.AreEqual(1, res.Count);

                // this one too
                res = trades.Where(t => t.IsDestroyed == false && t.MaturityDate == DateTime.Today).OnlyIfComplete().ToList();
                Assert.AreEqual(1, res.Count);

                // this one thrown an exception as the query is not a subset of the domain
                Assert.Throws<CacheException>(() =>
                        res = trades.Where(t => t.IsDestroyed == false && t.Portfolio == "SW-EUR").OnlyIfComplete().ToList()
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


        [Test]
        public void Test_trade_provider()
        {
            var provider = new TradeProvider();

            try
            {
                provider.Startup(_clientConfig);

                var trade = provider.GetTrade(11);

                Assert.IsNotNull(trade);
                Assert.IsFalse(provider.LastOneWasFromCache);

                trade = provider.GetTrade(11);

                Assert.IsNotNull(trade);
                Assert.IsTrue(provider.LastOneWasFromCache);
            }
            finally
            {
                provider.Shutdown();    
            }
        }

    }
}