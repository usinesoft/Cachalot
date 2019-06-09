using System;
using System.IO;
using System.Linq;
using Cachalot.Linq;
using Client.Interface;
using NUnit.Framework;
using Server.Persistence;
using UnitTests.TestData;
using UnitTests.TestData.Events;

// ReSharper disable ReturnValueOfPureMethodIsNotUsed

namespace UnitTests
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
                var events = connector.DataSource<ProductEvent>();

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
                var events = connector.DataSource<ProductEvent>();

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
                var events = connector.DataSource<ProductEvent>();

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
                var events = connector.DataSource<ProductEvent>();

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
                var dataSource = connector.DataSource<ProductEvent>();

                dataSource.PutMany(new ProductEvent[]
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
                var dataSource = connector.DataSource<ProductEvent>();

                dataSource.PutMany(new ProductEvent[]
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
                var dataSource = connector.DataSource<ProductEvent>();

                dataSource.PutMany(new ProductEvent[]
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
                var dataSource = connector.DataSource<ProductEvent>();


                // check same data after reload
                var events = dataSource.Where(evt => evt.EventType == "FIXING").ToList();
                Assert.AreEqual(1, events.Count);

                dataSource.DeleteMany(evt => evt.EventType == "FIXING");

                events = dataSource.Where(evt => evt.EventType == "FIXING").ToList();
                Assert.AreEqual(0, events.Count);
            }


            using (var connector = new Connector(config))
            {
                var dataSource = connector.DataSource<ProductEvent>();


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
                var dataSource = connector.DataSource<ProductEvent>();

                dataSource.PutMany(new ProductEvent[]
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
                var dataSource = connector.DataSource<ProductEvent>();

                dataSource.PutMany(new ProductEvent[]
                {
                    new FixingEvent(1, "AXA", 150, "EQ-256"),
                    new FixingEvent(2, "TOTAL", 180, "IRD-400"),
                    new Increase(3, 180, "EQ-256")
                });

                var newOne = (FixingEvent) dataSource[2];

                Assert.AreEqual(180, newOne.Value);

                dataSource.PutMany(new ProductEvent[]
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
                var dataSource = connector.DataSource<ProductEvent>();

                dataSource.PutMany(new ProductEvent[]
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
                var dataSource = connector.DataSource<ProductEvent>();

                dataSource.PutMany(new ProductEvent[]
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
    }
}