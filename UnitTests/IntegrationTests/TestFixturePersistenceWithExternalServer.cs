using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using Cachalot.Linq;
using Client.Core;
using Client.Core.Linq;
using Client.Interface;
using NUnit.Framework;
using Tests.TestData;
using Tests.TestData.Events;
using Trade = Tests.TestData.Instruments.Trade;
// ReSharper disable RedundantAssignment

namespace Tests.IntegrationTests
{
    [TestFixture]
    [Category("Performance")]
    //[Ignore("Starting an external server does not work on git ub actions")]
    public class TestFixturePersistenceWithExternalServer
    {
        private Process _process;

        [OneTimeSetUp]
        public void StartServer()
        {
#if DEBUG
            var path = "../../../../bin/Debug/Server/netcoreapp3.1";
#else
                var path = "../../../../bin/Release/Server/netcoreapp3.1";
#endif

            var fullPath = Path.Combine(path, "server.dll");

            Directory.GetCurrentDirectory();

            _process = new Process
            {
                StartInfo = { FileName = "dotnet", Arguments = $"{fullPath}", WorkingDirectory = path, WindowStyle = ProcessWindowStyle.Normal }
            };

            _process.Start();
        }


        [OneTimeTearDown]
        public void StopServer()
        {

            _process.Kill(true);
            _process.WaitForExit();
        }

        private readonly ClientConfig _config = new ClientConfig
        {
            IsPersistent = true,
            Servers =
            {
                new ServerConfig
                {
                    Host = "localhost",
                    Port = Constants.DefaultPort
                }
            }
        };

        


        [Test]
        public void Both_IPV6_and_IPV4_addresses_are_accepted()
        {
            // localhost
            var config = new ClientConfig
            {
                IsPersistent = true,
                Servers =
                {
                    new ServerConfig
                    {
                        Host = "localhost",
                        Port = Constants.DefaultPort
                    }
                }
            };


            using (var connector = new Connector(config))
            {
                connector.GenerateUniqueIds("event", 1);
            }


            // IPV4
            config = new ClientConfig
            {
                IsPersistent = true,
                Servers =
                {
                    new ServerConfig
                    {
                        Host = "127.0.0.1",
                        Port = Constants.DefaultPort
                    }
                }
            };


            using (var connector = new Connector(config))
            {
                connector.GenerateUniqueIds("event", 1);
            }


            // IPV6
            config = new ClientConfig
            {
                IsPersistent = true,
                Servers =
                {
                    new ServerConfig
                    {
                        Host = "::1",
                        Port = Constants.DefaultPort
                    }
                }
            };


            using (var connector = new Connector(config))
            {
                connector.GenerateUniqueIds("event", 1);
            }

            // works with connection string too
            using (var connector = new Connector($"localhost:{Constants.DefaultPort}"))
            {
                connector.GenerateUniqueIds("event", 1);
            }
        }


        [Test]
        public void Create_trades_and_apply_events()
        {
            using (var connector = new Connector(_config))
            {
                
                connector.AdminInterface().DropDatabase();

                connector.DeclareCollection<Event>();
                connector.DeclareCollection<Trade>();


                var events = connector.DataSource<Event>();
                var trades = connector.DataSource<Trade>();

                var factory = new ProductFactory(connector);

                var (trade, evt) =
                    factory.CreateOption(10, 100, "GOLDMAN.LDN", "OPTEUR", "AXA", 100, false, true, false, 6);

                events.Put(evt);
                trades.Put(trade);

                var tradeReloaded = trades.Single(t => t.ContractId == trade.ContractId);
                var eventReloaded = events.Single(e => e.DealId == trade.ContractId);

                Assert.AreEqual(tradeReloaded.Id, trade.Id);
                Assert.AreEqual(eventReloaded.EventId, evt.EventId);

                // apply an increase event
                var (newVersion, increase) =
                    factory.IncreaseOption(trade, 50);

                trades.Put(trade);
                trades.Put(newVersion);
                events.Put(increase);

                var allVersions = trades.Where(t => t.ContractId == trade.ContractId).ToList()
                    .OrderBy(t => t.Version).ToList();

                Assert.AreEqual(2, allVersions.Count);
                Assert.AreEqual(1, allVersions[0].Version);
                Assert.AreEqual(2, allVersions[1].Version);
                Assert.IsTrue(allVersions[1].IsLastVersion);
                Assert.IsFalse(allVersions[0].IsLastVersion);
            }
        }


        [Test]
        public void Dump_different_types_of_objects()
        {
            using (var connector = new Connector(_config))
            {
                
                connector.AdminInterface().DropDatabase();

                connector.DeclareCollection<Home>();

                var ids = connector.GenerateUniqueIds("home_id", 100);

                var list = new List<Home>();
                for (var i = 0; i < 100; i++)
                {
                    var home = new Home
                    {
                        Id = ids[i],
                        Town = "Paris",
                        CountryCode = "FR",
                        Address = "rue des malheurs",
                        Bathrooms = 1,
                        Rooms = 2
                    };
                    list.Add(home);
                }

                var homes = connector.DataSource<Home>();
                homes.PutMany(list);

                connector.AdminInterface().Dump("dump");
            }
        }

        [Test]
        public void Full_text_search()
        {
            using (var connector = new Connector(_config))
            {
                connector.AdminInterface().DropDatabase();
                connector.DeclareCollection<Home>();

                var ids = connector.GenerateUniqueIds("home_id", 103);

                var list = new List<Home>();
                for (var i = 0; i < 100; i++)
                {
                    var home = new Home
                    {
                        Id = ids[i],
                        Town = "Paris",
                        CountryCode = "FR",
                        Address = "rue des malheurs",
                        Bathrooms = 1,
                        Rooms = 2
                    };
                    list.Add(home);
                }

                var homes = connector.DataSource<Home>();
                homes.PutMany(list);

               
                // manually add some items for full-text search testing
                var h1 = new Home
                {
                    Id = ids[ids.Length - 3],
                    Address = "14 rue de la mort qui tue",
                    Bathrooms = 2,
                    CountryCode = "FR",
                    PriceInEuros = 150,
                    Rooms = 3,
                    Town = "Paris",
                    Comments = new List<Comment>
                    {
                        new Comment{Text="beautiful view"},
                        new Comment{Text="close to the metro"},
                    }
                };

                var h2 = new Home
                {
                    Id = ids[ids.Length - 2],
                    Address = "15 allée de l'amour",
                    Bathrooms = 1,
                    CountryCode = "FR",
                    PriceInEuros = 250,
                    Rooms = 4,
                    Town = "Paris",
                    Comments = new List<Comment>
                    {
                        new Comment{Text="ps4"},
                        new Comment{Text="close to the metro"},
                    }
                };

                var h3 = new Home
                {
                    Id = ids[ids.Length - 1],
                    Address = "156 db du gral Le Clerc",
                    Bathrooms = 2,
                    CountryCode = "FR",
                    PriceInEuros = 200,
                    Rooms = 3,
                    Town = "Nice",
                    Comments = new List<Comment>
                    {
                        new Comment{Text="wonderful sea view"},
                        new Comment{Text="close to beach"},
                    }
                };

                homes.Put(h1);
                homes.Put(h2);
                homes.Put(h3);

                var result = homes.FullTextSearch("gral le clerc");
                Assert.AreEqual(h3.Id, result.First().Id);

                result = homes.FullTextSearch("amour");
                Assert.AreEqual(h2.Id, result.First().Id);
            }
        }


        [Test]
        public void Take_extension_methods()
        {
            const int items = 10000;
            using (var connector = new Connector(_config))
            {
                connector.AdminInterface().DropDatabase();
                connector.DeclareCollection<Event>();

                var dataSource = connector.DataSource<Event>();

                var ids = connector.GenerateUniqueIds("event", items);

                var eventDate = DateTime.Today.AddYears(-10);
                var events = new List<Event>();
                for (var i = 0; i < items; i++)
                {
                    switch (i % 3)
                    {
                        case 0:
                            events.Add(new FixingEvent(ids[i], "AXA", 150, "EQ-256")
                                {EventDate = eventDate, ValueDate = eventDate.AddDays(2)});
                            break;
                        case 1:
                            events.Add(new FixingEvent(ids[i], "TOTAL", 180, "IRD-400")
                                {EventDate = eventDate, ValueDate = eventDate.AddDays(2)});
                            break;
                        case 2:
                            events.Add(new Increase(ids[i], 180, "EQ-256")
                                {EventDate = eventDate, ValueDate = eventDate.AddDays(2)});
                            break;
                    }

                    eventDate = eventDate.AddDays(1);
                }

                dataSource.PutMany(events);


                var list = dataSource.Where(e => e.EventType == "FIXING").Take(10).ToList();
                Assert.AreEqual(10, list.Count);
            }
        }


        [Test]
        public void Feed_data_and_compute_pivot()
        {
            const int items = 100000;
            using (var connector = new Connector(_config))
            {
                connector.AdminInterface().DropDatabase();

                connector.DeclareCollection<Order>();

                var dataSource = connector.DataSource<Order>();


                List<Order> orders = new List<Order>();

                // generate orders for three categories
                for (int i = 0; i < items; i++)
                {
                    var order = new Order{Id = Guid.NewGuid(), Amount = 10.15, ClientId = 100 + i+10, Date = DateTimeOffset.Now, Category = "geek", ProductId = 1000 + i%10, Quantity = 2};

                    if (i % 5 == 0)
                    {
                        order.Category = "sf";
                    }
                    else if(i % 5 == 1)
                    {
                        order.Category = "science";
                    }

                    orders.Add(order);
                }

                

                dataSource.PutMany(orders);


                var watch = new Stopwatch();
                watch.Start();
                
                var pivot = dataSource.PreparePivotRequest()
                    .OnAxis(o => o.Category, o => o.ProductId)
                    .AggregateValues(o=>o.Amount, o=>o.Quantity)
                    .Execute();

                watch.Stop();

                Console.WriteLine($"Computing pivot table for {items} objects took {watch.ElapsedMilliseconds} milliseconds");
                
                Assert.AreEqual(3, pivot.Children.Count, "3 categories should have been returned"); 

                pivot.CheckPivot();

                Console.WriteLine(pivot);
            }
        }

        [Test]
        public void Order_by_with_single_server()
        {
            
            using var connector = new Connector(_config);

            connector.AdminInterface().DropDatabase();

            connector.DeclareCollection<Order>();

            var dataSource = connector.DataSource<Order>();

            List<Order> orders = Order.GenerateTestData(10_000);

            
            dataSource.PutMany(orders);

            // warm up
            var _ = dataSource.Where(o => o.Category == "geek").ToList();
            _ = dataSource.Where(o => o.Category == "geek").OrderBy(o=>o.Amount).ToList();
            _ = dataSource.Where(o => o.Category == "geek").OrderByDescending(o=>o.Amount).ToList();

            var watch = new Stopwatch();
            watch.Start();

            var noOrder = dataSource.Where(o => o.Category == "geek").ToList();

            Console.WriteLine($"Getting {noOrder.Count} objects without order-by took {watch.ElapsedMilliseconds} milliseconds");

            watch.Restart();

            var ascending = dataSource.Where(o => o.Category == "geek").OrderBy(o=>o.Amount).ToList();

            Console.WriteLine($"Getting {ascending.Count} objects with order-by took {watch.ElapsedMilliseconds} milliseconds");
                
            Assert.AreEqual(noOrder.Count, ascending.Count); 

            watch.Restart();

            var descending = dataSource.Where(o => o.Category == "geek").OrderByDescending(o=>o.Amount).ToList();

            Console.WriteLine($"Getting {descending.Count} objects with order-by descending took {watch.ElapsedMilliseconds} milliseconds");
                
            Assert.AreEqual(noOrder.Count, descending.Count); 

            // check that they are ordered
            
            // check sorted ascending
            for (int i = 0; i < ascending.Count - 1; i++)
            {
                Assert.LessOrEqual((int)ascending[i].Amount*10000, (int)ascending[i+1].Amount *10000);
            }

            // check sorted descending
            for (int i = 0; i < descending.Count - 1; i++)
            {
                Assert.GreaterOrEqual((int)descending[i].Amount*10000, (int)descending[i+1].Amount *10000);
            }

            watch.Stop();

            
        }

    }
}