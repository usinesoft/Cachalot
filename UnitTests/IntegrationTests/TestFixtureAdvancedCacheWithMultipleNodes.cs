using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Cachalot.Linq;
using Channel;
using Client;
using Client.Core.Linq;
using Client.Interface;
using NUnit.Framework;
using Server;
using Tests.TestData;
using Tests.TestData.Events;
using Trade = Tests.TestData.Instruments.Trade;

namespace Tests.IntegrationTests
{
    [TestFixture]
    public class TestFixtureAdvancedCacheWithMultipleNodes
    {
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

        private class ServerInfo
        {
            public TcpServerChannel Channel { get; set; }
            public Server.Server Server { get; set; }
            public int Port { get; set; }
        }

        private List<ServerInfo> _servers = new List<ServerInfo>();

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

        private ClientConfig _clientConfig;

        private void StartServers(int serverCount = 0)
        {

            try
            {
                Trace.WriteLine("starting servers");

            
                _clientConfig = new ClientConfig();
                _servers = new List<ServerInfo>();

                serverCount = serverCount == 0 ? ServerCount : serverCount;

                for (var i = 0; i < serverCount; i++)
                {
                    var serverInfo = new ServerInfo {Channel = new TcpServerChannel()};
                    serverInfo.Server = new Server.Server(new NodeConfig{DataPath = $"server{i:D2}"})
                        {Channel = serverInfo.Channel}; // start non-persistent server
                    serverInfo.Port = serverInfo.Channel.Init(); // get the dynamically allocated ports
                    Trace.WriteLine($"starting server on port {serverInfo.Port}");
                
                    serverInfo.Channel.Start();
                    Trace.WriteLine("channel started");
                    serverInfo.Server.Start();
                    Trace.WriteLine("starting servers");

                    _servers.Add(serverInfo);

                    _clientConfig.Servers.Add(
                        new ServerConfig {Host = "localhost", Port = serverInfo.Port});
                }

            }
            catch (Exception e)
            {
                Trace.WriteLine(e);
                throw;
            }

            Thread.Sleep(500); //be sure the server nodes are started
        }

        [Test]
        public void Domain_declaration_example()
        {
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Home>();
                connector.DeclareCollection<Trade>();

                var homes = connector.DataSource<Home>();
                

                homes.Put(new Home
                    {Id = 1, CountryCode = "FR", PriceInEuros = 150, Town = "Paris", Rooms = 3, Bathrooms = 1});
                homes.Put(new Home
                    {Id = 2, CountryCode = "FR", PriceInEuros = 250, Town = "Paris", Rooms = 5, Bathrooms = 2});
                homes.Put(new Home
                    {Id = 3, CountryCode = "FR", PriceInEuros = 100, Town = "Nice", Rooms = 1, Bathrooms = 1});
                homes.Put(new Home
                    {Id = 4, CountryCode = "FR", PriceInEuros = 150, Town = "Nice", Rooms = 2, Bathrooms = 1});

                homes.DeclareLoadedDomain(h => h.Town == "Paris" || h.Town == "Nice");

                // this one can be served from cache
                var result = Enumerable.ToList(homes.Where(h => h.Town == "Paris" && h.Rooms >= 2).OnlyIfComplete());

                Assert.AreEqual(2, result.Count);

                // this one too
                result = Enumerable.ToList(homes.Where(h => h.Town == "Nice" && h.Rooms == 2).OnlyIfComplete());

                Assert.AreEqual(1, result.Count);

                // this one thrown an exception as the query is not a subset of the domain
                Assert.Throws<CacheException>(() =>
                    result = homes.Where(h => h.CountryCode == "FR" && h.Rooms == 2).OnlyIfComplete().ToList()
                );


                var trades = connector.DataSource<Trade>();

                trades.Put(new Trade
                {
                    Id = 1, ContractId = "SWAP-001", Counterparty = "BNP", TradeDate = DateTime.Today,
                    MaturityDate = DateTime.Today
                });
                trades.Put(new Trade
                {
                    Id = 2, ContractId = "SWAP-002", Counterparty = "GOLDMAN", TradeDate = DateTime.Today.AddDays(-1),
                    MaturityDate = DateTime.Today.AddDays(100)
                });
                trades.Put(new Trade
                {
                    Id = 3, ContractId = "SWAP-003", Counterparty = "BNP", TradeDate = DateTime.Today.AddDays(-2),
                    MaturityDate = DateTime.Today.AddDays(50), IsDestroyed = true
                });
                trades.Put(new Trade
                {
                    Id = 4, ContractId = "SWAP-004", Counterparty = "MLINCH", TradeDate = DateTime.Today.AddDays(-3),
                    MaturityDate = DateTime.Today.AddDays(15)
                });


                var oneYearAgo = DateTime.Today.AddYears(-1);
                var today = DateTime.Today;

                trades.DeclareLoadedDomain(t => t.MaturityDate >= today || t.TradeDate > oneYearAgo);

                // this one can be served from cache
                var res = Enumerable.ToList(trades
                    .Where(t => t.IsDestroyed == false && t.TradeDate == DateTime.Today.AddDays(-1)).OnlyIfComplete());
                Assert.AreEqual(1, res.Count);

                // this one too
                res = Enumerable.ToList(trades.Where(t => t.IsDestroyed == false && t.MaturityDate == DateTime.Today)
                    .OnlyIfComplete());
                Assert.AreEqual(1, res.Count);

                // this one thrown an exception as the query is not a subset of the domain
                Assert.Throws<CacheException>(() =>
                    res = Enumerable.ToList(trades.Where(t => t.IsDestroyed == false && t.Portfolio == "SW-EUR")
                        .OnlyIfComplete())
                );
            }
        }

        [Test]
        public void Feed_cache_and_declare_domain_completion()
        {
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<ProductEvent>();

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
                    Enumerable.ToList(dataSource.Where(e => e.EventType == "FIXING").OnlyIfComplete())
                );


                // declare that all data is available
                dataSource.DeclareFullyLoaded();

                var fixings = Enumerable.ToList(dataSource.Where(e => e.EventType == "FIXING").OnlyIfComplete());
                Assert.Greater(fixings.Count, 0);


                // declare that data is not available again
                dataSource.DeclareFullyLoaded(false);

                Assert.Throws<CacheException>(() =>
                    // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                    Enumerable.ToList(dataSource.Where(e => e.EventType == "FIXING").OnlyIfComplete())
                );


                // declare that all fixings are available
                dataSource.DeclareLoadedDomain(p => p.EventType == "FIXING");
                fixings = Enumerable.ToList(dataSource
                    .Where(e => e.EventType == "FIXING" && e.EventDate == DateTime.Today).OnlyIfComplete());
                Assert.Greater(fixings.Count, 0);

                // the next query can not be guaranteed to be complete


                Assert.Throws<CacheException>(() =>
                    // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                    Enumerable.ToList(dataSource.Where(e => e.EventDate == DateTime.Today).OnlyIfComplete())
                );
            }
        }


        [Test]
        public void Feed_cache_and_get_data()
        {
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<ProductEvent >();

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
        public void Test_lru_eviction()
        {
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Trade>();

                var trades = connector.DataSource<Trade>();
                trades.ConfigEviction(EvictionType.LessRecentlyUsed, 50, 5);


                // check that eviction is triggered by individual PUT operations
                for (var i = 0; i < 100; i++)
                {
                    var trade = new Trade
                    {
                        Id = i, ContractId = $"TRD-{i}", Counterparty = "YOU", IsLastVersion = true, Portfolio = "PTF44"
                    };
                    trades.Put(trade);
                }

                var count = trades.Count();
                Assert.LessOrEqual(count, 50);

                // check that eviction is triggered by PUT MANY operations
                var list = new List<Trade>();
                for (var i = 100; i < 200; i++)
                {
                    var trade = new Trade
                    {
                        Id = i, ContractId = $"TRD-{i}", Counterparty = "YOU", IsLastVersion = true, Portfolio = "PTF44"
                    };
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

        [Test]
        public void Test_pivot()
        {
            const int items = 100000;
            using (var connector = new Connector(_clientConfig))
            {
                connector.AdminInterface().DropDatabase();

                connector.DeclareCollection<Order>();

                var dataSource = connector.DataSource<Order>();


                List<Order> orders = new List<Order>();

                // generate orders for three categories
                for (int i = 0; i < items; i++)
                {
                    var order = new Order
                    {
                        Id = Guid.NewGuid(), Amount = 10.15, ClientId = 100 + i + 10, Date = DateTimeOffset.Now,
                        Category = "geek", ProductId = 1000 + i % 10, Quantity = 2
                    };

                    if (i % 5 == 0)
                    {
                        order.Category = "sf";
                    }
                    else if (i % 5 == 1)
                    {
                        order.Category = "science";
                    }

                    orders.Add(order);
                }



                dataSource.PutMany(orders);


                var watch = new Stopwatch();
                watch.Start();
                
                var pivot = dataSource.PreparePivotRequest(null).OnAxis(o => o.Category, o => o.ProductId).AggregateValues(o=>o.Amount, o=>o.Quantity).Execute();

                watch.Stop();

                Console.WriteLine(
                    $"Computing pivot table for {items} objects took {watch.ElapsedMilliseconds} milliseconds");

                Assert.AreEqual(3, pivot.Children.Count, "3 categories should have been returned");

                pivot.CheckPivot();

                Console.WriteLine(pivot);
            }
        }
    }
}