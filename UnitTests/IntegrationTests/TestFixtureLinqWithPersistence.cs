using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cachalot.Linq;
using Client.Core.Linq;
using Client.Interface;
using NUnit.Framework;
using Server.Persistence;
using Tests.TestData;
using Tests.TestData.Events;

// ReSharper disable ReturnValueOfPureMethodIsNotUsed

namespace Tests.IntegrationTests
{
    [TestFixture]
    public class TestFixtureLinqWithPersistence
    {
        [SetUp]
        public void SetUp()
        {
            if (Directory.Exists(Constants.DataPath)) Directory.Delete(Constants.DataPath, true);
        }

        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            Environment.CurrentDirectory = TestContext.CurrentContext.TestDirectory;
            Directory.SetCurrentDirectory(TestContext.CurrentContext.TestDirectory);
        }


        [Test]
        public void Conditional_put()
        {
            var config = new ClientConfig();
            config.LoadFromFile("inprocess_persistent_config.xml");

            using (var connector = new Connector(config))
            {
                connector.DeclareCollection<Event>();

                var events = connector.DataSource<Event>();

                var wasAdded = events.TryAdd(new FixingEvent(1, "AXA", 150, "EQ-256"));

                Assert.IsTrue(wasAdded);

                // the second time it should return false as it is already there
                wasAdded = events.TryAdd(new FixingEvent(1, "AXA", 160, "EQ-256"));

                Assert.IsFalse(wasAdded);

                var reloaded = (FixingEvent) events[1];

                // check that the original value is still there
                Assert.AreEqual(150, reloaded.Value);
            }

            // check also that it has not been saved in the persistence storage
            using (var connector = new Connector(config))
            {
                connector.DeclareCollection<Event>();

                var events = connector.DataSource<Event>();

                var reloaded = (FixingEvent) events[1];

                // check that the original value is still there
                Assert.AreEqual(150, reloaded.Value);
            }
        }

        [Test]
        public void Conditional_update()
        {
            var config = new ClientConfig();
            config.LoadFromFile("inprocess_persistent_config.xml");

            using (var connector = new Connector(config))
            {
                connector.DeclareCollection<Event>();

                var events = connector.DataSource<Event>();

                var wasAdded = events.TryAdd(new FixingEvent(1, "AXA", 150, "EQ-256") {Timestamp = DateTime.Now});

                Assert.IsTrue(wasAdded);

                var reloaded = (FixingEvent) events[1];
                var oldTimestamp = reloaded.Timestamp;

                reloaded.Value = 160;
                reloaded.Timestamp = DateTime.Now.AddTicks(1); // to be sure we are not too fast

                Assert.AreNotEqual(oldTimestamp.Ticks, reloaded.Timestamp.Ticks);

                events.UpdateIf(reloaded, evt => evt.Timestamp == oldTimestamp);

                // try a new conditional update that should fail because the object was already updated

                reloaded.Value = 111;

                Assert.Throws<CacheException>(() => events.UpdateIf(reloaded, evt => evt.Timestamp == oldTimestamp));
            }

            // check also that it has not been saved in the persistence storage
            using (var connector = new Connector(config))
            {
                connector.DeclareCollection<Event>();

                var events = connector.DataSource<Event>();

                var reloaded = (FixingEvent) events[1];

                // check that the updated value is persistent
                Assert.AreEqual(160, reloaded.Value);
            }
        }


        [Test]
        public void Generate_lots_of_transactions_stop_the_server_quickly_to_generate_pending_transactions()
        {
            var config = new ClientConfig();
            config.LoadFromFile("inprocess_persistent_config.xml");

            using (var connector = new Connector(config))
            {
                connector.DeclareCollection<Trade>();

                var dataSource = connector.DataSource<Trade>();


                for (var i = 0; i < 1000; i++)
                    if (i % 10 == 0)
                        dataSource.Put(new Trade(i, 1000 + i, "TOTO", DateTime.Now.Date, 150));
                    else
                        dataSource.Put(new Trade(i, 1000 + i, "TATA", DateTime.Now.Date, 150));
            }

            // for an in-process configuration disposing the connector will dispose the server
            using (var connector = new Connector(config))
            {
                connector.DeclareCollection<Trade>();

                var dataSource = connector.DataSource<Trade>();

                {
                    var folders = new[] {"TATA", "TOTO"};


                    var list = dataSource.Where(t => folders.Contains(t.Folder)).ToList();

                    Assert.AreEqual(1000, list.Count);

                    list = dataSource.Where(t => t.Folder == "TOTO").ToList();

                    Assert.AreEqual(100, list.Count);

                    var t1 = dataSource[4];
                    Assert.IsNotNull(t1);
                    Assert.AreEqual(4, t1.Id);
                }
            }
        }

        [Test]
        public void Generate_unique_ids()
        {
            var config = new ClientConfig();
            config.LoadFromFile("inprocess_persistent_config.xml");

            var max = 0;
            using (var connector = new Connector(config))
            {
                

                var ids = connector.GenerateUniqueIds("test", 12);

                Assert.AreEqual(12, ids.Length);

                CollectionAssert.AllItemsAreUnique(ids, "identifiers are not unique");

                max = ids.Max();

                Assert.IsTrue(ids.Min() > 0, "unique ids should be strictly positive");
            }


            // check that after restart unique ids are bigger than the previous ones
            using (var connector = new Connector(config))
            {
                var ids = connector.GenerateUniqueIds("test", 12);

                Assert.AreEqual(12, ids.Length);

                CollectionAssert.AllItemsAreUnique(ids, "identifiers are not unique");


                Assert.IsTrue(ids.Min() > max, "sequence persistence failure");
            }
        }

        [Test]
        public void Get_all()
        {
            var config = new ClientConfig();
            config.LoadFromFile("inprocess_persistent_config.xml");

            using (var connector = new Connector(config))
            {
                connector.DeclareCollection<Event>();

                var dataSource = connector.DataSource<Event>();

                dataSource.PutMany(new Event[]
                {
                    new FixingEvent(1, "AXA", 150, "EQ-256"),
                    new FixingEvent(2, "TOTAL", 180, "IRD-400"),
                    new Increase(3, 180, "EQ-256")
                });

                var list = dataSource.ToList();

                Assert.AreEqual(3, list.Count);
            }
        }

        [Test]
        public void Null_values_can_be_used_in_index()
        {
            var config = new ClientConfig();
            config.LoadFromFile("inprocess_persistent_config.xml");

            using (var connector = new Connector(config))
            {
                connector.DeclareCollection<Event>();

                var dataSource = connector.DataSource<Event>();

                dataSource.PutMany(new Event[]
                {
                    new FixingEvent(1, "AXA", 150, "EQ-256"),
                    new FixingEvent(2, "TOTAL", 180, null),
                    new Increase(3, 180, "EQ-256")
                });


                var count = dataSource.Count(evt => evt.DealId == "EQ-256");

                Assert.AreEqual(2, count);

                count = dataSource.Count(evt => evt.DealId == null);

                Assert.AreEqual(1, count);
            }
        }


        [Test]
        public void Polymorphic_collection()
        {
            var config = new ClientConfig();
            config.LoadFromFile("inprocess_persistent_config.xml");


            using (var connector = new Connector(config))
            {
                connector.DeclareCollection<Event>();
                var dataSource = connector.DataSource<Event>();

                dataSource.PutMany(new Event[]
                {
                    new FixingEvent(1, "AXA", 150, "EQ-256"),
                    new FixingEvent(2, "TOTAL", 180, "IRD-400"),
                    new Increase(3, 180, "EQ-256")
                });


                var events = dataSource.Where(evt => evt.DealId == "EQ-256").ToList();

                Assert.AreEqual(2, events.Count);

                events = dataSource.Where(evt => evt.EventType == "FIXING").ToList();

                Assert.AreEqual(2, events.Count);

                // check that empty result is managed
                var count = dataSource.Count(e => e.EventId == 3 && e.DealId == "none");
                Assert.AreEqual(0, count);

                var empty = dataSource.Where(e => e.EventId == 3 && e.DealId == "none").ToList();
                Assert.IsEmpty(empty);

                // delete one fixing event
                dataSource.Delete(events[0]);

                events = dataSource.Where(evt => evt.EventType == "FIXING").ToList();

                Assert.AreEqual(1, events.Count);
            }


            using (var connector = new Connector(config))
            {
                connector.DeclareCollection<Event>();

                var dataSource = connector.DataSource<Event>();


                // check same data after reload
                var events = dataSource.Where(evt => evt.EventType == "FIXING").ToList();
                Assert.AreEqual(1, events.Count);

                dataSource.DeleteMany(evt => evt.EventType == "FIXING");

                events = dataSource.Where(evt => evt.EventType == "FIXING").ToList();
                Assert.AreEqual(0, events.Count);
            }


            using (var connector = new Connector(config))
            {
                connector.DeclareCollection<Event>();
                var dataSource = connector.DataSource<Event>();


                // check same data after reload
                var events = dataSource.Where(evt => evt.EventType == "FIXING").ToList();
                Assert.AreEqual(0, events.Count);
            }
        }

        [Test]
        public void Process_extensions_that_are_supported_only_locally()
        {
            var config = new ClientConfig();
            config.LoadFromFile("inprocess_persistent_config.xml");

            using (var connector = new Connector(config))
            {
                connector.DeclareCollection<Event>();

                var dataSource = connector.DataSource<Event>();

                dataSource.PutMany(new Event[]
                {
                    new FixingEvent(1, "AXA", 150, "EQ-256"),
                    new FixingEvent(2, "TOTAL", 180, "IRD-400"),
                    new Increase(3, 180, "EQ-256")
                });


                var deals = dataSource.ToList().Select(e => e.DealId).Distinct().ToList();
                Assert.AreEqual(2, deals.Count);

                deals = dataSource.Where(e => e.EventType == "FIXING").ToList().Select(e => e.DealId).Distinct()
                    .ToList();
                Assert.AreEqual(2, deals.Count);
            }
        }

        [Test]
        public void Put_data_then_update_then_reload()
        {
            var config = new ClientConfig();
            config.LoadFromFile("inprocess_persistent_config.xml");

            using (var connector = new Connector(config))
            {
                connector.DeclareCollection<Trade>();

                var dataSource = connector.DataSource<Trade>();

                dataSource.Put(new Trade(1, 5465, "TATA", DateTime.Now.Date, 150));
                dataSource.Put(new Trade(3, 5467, "TATA", DateTime.Now.Date.AddDays(-1), 150));


                {
                    var folders = new[] {"TATA", "TOTO"};

                    var list = dataSource.Where(t => folders.Contains(t.Folder)).ToList();

                    Assert.AreEqual(2, list.Count);

                    var t1 = dataSource[1];
                    Assert.IsNotNull(t1);
                    Assert.AreEqual(1, t1.Id);
                }

                dataSource.Put(new Trade(4, 5468, "TOTO", DateTime.Now.Date.AddDays(+1), 150));

                {
                    var folders = new[] {"TATA", "TOTO"};

                    var list = dataSource.Where(t => folders.Contains(t.Folder)).ToList();

                    Assert.AreEqual(3, list.Count);

                    var t1 = dataSource[4];
                    Assert.IsNotNull(t1);
                    Assert.AreEqual(4, t1.Id);
                }
            }

            // for an in-process configuration disposing the connector will dispose the server
            using (var connector = new Connector(config))
            {
                connector.DeclareCollection<Trade>();
                var dataSource = connector.DataSource<Trade>();

                {
                    var folders = new[] {"TATA", "TOTO"};


                    var list = dataSource.Where(t => folders.Contains(t.Folder)).ToList();

                    Assert.AreEqual(3, list.Count);

                    var t1 = dataSource[4];
                    Assert.IsNotNull(t1);
                    Assert.AreEqual(4, t1.Id);
                }
            }
        }

        [Test]
        public void Put_many_with_update()
        {
            var config = new ClientConfig();
            config.LoadFromFile("inprocess_persistent_config.xml");

            using (var connector = new Connector(config))
            {
                connector.DeclareCollection<Event>();

                var dataSource = connector.DataSource<Event>();

                dataSource.PutMany(new Event[]
                {
                    new FixingEvent(1, "AXA", 150, "EQ-256"),
                    new FixingEvent(2, "TOTAL", 180, "IRD-400"),
                    new Increase(3, 180, "EQ-256")
                });

                var newOne = (FixingEvent) dataSource[2];

                Assert.AreEqual(180, newOne.Value);

                dataSource.PutMany(new Event[]
                {
                    new FixingEvent(2, "TOTAL", 190, "IRD-400"),
                    new Increase(3, 190, "EQ-256")
                });


                var updated = (FixingEvent) dataSource[2];

                Assert.AreEqual(190, updated.Value);
            }
        }


        [Test]
        public void Scalar_queries()
        {
            var config = new ClientConfig();
            config.LoadFromFile("inprocess_persistent_config.xml");

            using (var connector = new Connector(config))
            {
                connector.DeclareCollection<Event>();

                var dataSource = connector.DataSource<Event>();

                dataSource.PutMany(new Event[]
                {
                    new FixingEvent(1, "AXA", 150, "EQ-256"),
                    new FixingEvent(2, "TOTAL", 180, "IRD-400"),
                    new Increase(3, 180, "EQ-256")
                });

                var count = dataSource.Count(evt => evt.DealId == "EQ-256");

                Assert.AreEqual(2, count);
            }
        }

        [Test]
        public void Take_and_skip()
        {
            var config = new ClientConfig();
            config.LoadFromFile("inprocess_persistent_config.xml");

            using (var connector = new Connector(config))
            {
                connector.DeclareCollection<Event>();

                var dataSource = connector.DataSource<Event>();

                dataSource.PutMany(new Event[]
                {
                    new FixingEvent(1, "AXA", 150, "EQ-256"),
                    new FixingEvent(2, "TOTAL", 180, "IRD-400"),
                    new FixingEvent(3, "TOTAL", 190, "IRD-400"),
                    new Increase(4, 180, "EQ-256")
                });

                var count = dataSource.Count(evt => evt.EventType == "FIXING");
                Assert.AreEqual(3, count);

                // check that count works with empty query
                count = dataSource.Count();
                Assert.AreEqual(4, count);


                var only2 = dataSource.Where(evt => evt.EventType == "FIXING").Take(2);

                // here we Count on the sever
                Assert.AreEqual(2, only2.Count());

                // here we count on the client
                var list = dataSource.Where(evt => evt.EventType == "FIXING").Take(2).ToList();
                Assert.AreEqual(2, list.Count);
            }
        }

         [Test]
        public void Full_text_search()
        {

            var config = new ClientConfig();
            config.LoadFromFile("inprocess_persistent_config.xml");

            using (var connector = new Connector(config))
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
                    Id = ids[^3],
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
                    Id = ids[^2],
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
                    Id = ids[^1],
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
        public void Multiple_collections_for_same_type()
        {
            
            var config = new ClientConfig();
            config.LoadFromFile("inprocess_persistent_config.xml");

            using var connector = new Connector(config);
                
            connector.AdminInterface().DropDatabase();

            connector.DeclareCollection<Home>("homes");
            connector.DeclareCollection<Home>("homes1");


            var homes = connector.DataSource<Home>("homes");
            var homes1 = connector.DataSource<Home>("homes1");

            homes.Put(new Home{Id = 15, CountryCode = "FR"});
            homes1.Put(new Home{Id = 15, CountryCode = "US"});
            homes1.Put(new Home{Id = 16, CountryCode = "US"});


            var all = homes.ToList();
            
            var all1 = homes1.ToList();
            
            Assert.AreEqual(1, all.Count);
            Assert.AreEqual(2, all1.Count);

        }

        [Test]
        public void Consistent_read()
        {
            
                var config = new ClientConfig();
                config.LoadFromFile("inprocess_persistent_config.xml");

                using var connector = new Connector(config);
                
                connector.AdminInterface().DropDatabase();

                connector.DeclareCollection<Home>("homes");
                connector.DeclareCollection<Home>("homes1");


                var homes = connector.DataSource<Home>("homes");
                var homes1 = connector.DataSource<Home>("homes1");

                homes.Put(new Home{Id = 15, CountryCode = "FR"});
                homes1.Put(new Home{Id = 15, CountryCode = "US"});
                homes1.Put(new Home{Id = 16, CountryCode = "US"});


                connector.ConsistentRead(ctx =>
                {
                    var all = ctx.Collection<Home>("homes").ToList();
                    Assert.AreEqual(1, all.Count);

                    // should throw an exception because "homes1" is not available in this context
                    Assert.Throws<NotSupportedException>(()=>
                    {
                        all = ctx.Collection<Home>("homes1").ToList();
                    });
                    

                }, "homes");

                connector.ConsistentRead(ctx =>
                {
                    var all = ctx.Collection<Home>("homes").ToList();
                    Assert.AreEqual(1, all.Count);

                    
                    all = ctx.Collection<Home>("homes1").ToList();
                    Assert.AreEqual(2, all.Count);
                    
                }, "homes", "homes1");
        }

        [Test]
        public void Update_items_with_put_many()
        {
            var config = new ClientConfig();
            config.LoadFromFile("inprocess_persistent_config.xml");

            using var connector = new Connector(config);

            connector.DeclareCollection<Order>();

            var dataSource = connector.DataSource<Order>();

            var testData = Order.GenerateTestData(10_000);

            dataSource.PutMany(testData);

            var reloaded0 = dataSource.Where(o => o.Category == "sf").ToList();

            reloaded0[0].Category = "vibes";

            dataSource.PutMany(reloaded0.Take(10)); // update less than the bulk insert threshold

            var reloaded1 = dataSource.Where(o => o.Category == "vibes").ToList();

            Assert.AreEqual(1, reloaded1.Count );

            var reloaded2 = dataSource.Where(o => o.Category == "sf").ToList();

            Assert.AreEqual(reloaded0.Count - 1, reloaded2.Count);

            reloaded0[1].Category = "vibes";
            dataSource.PutMany(reloaded0.Take(60)); // update more than the bulk insert threshold

            var reloaded3 = dataSource.Where(o => o.Category == "sf").ToList();

            Assert.AreEqual(reloaded0.Count - 2, reloaded3.Count);


        }

    }


}