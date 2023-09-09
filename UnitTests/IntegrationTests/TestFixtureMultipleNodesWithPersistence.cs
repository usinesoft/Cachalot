using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cachalot.Linq;
using Client;
using Client.Core;
using Client.Core.Linq;
using Client.Interface;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.TestData;
using Tests.TestData.Events;

namespace Tests.IntegrationTests
{
    [TestFixture]
    public class TestFixtureMultipleNodesWithPersistence : MultiServerTestFixtureBase
    {
        [TearDown]
        public void Exit()
        {
            TearDown();
        }

        [SetUp]
        public void Init()
        {
            SetUp();
        }

        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            OneTimeSetUp();
        }

        [Test]
        public void Check_that_the_order_of_returned_items_is_stable()
        {
            using var connector = new Connector(_clientConfig);
            connector.DeclareCollection<Event>();

            var dataSource = connector.DataSource<Event>();

            var events = new List<Event>();
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


            var fixings = dataSource.Where(e => e.EventType == "FIXING").ToList();
            for (var i = 0; i < 10; i++)
            {
                var sameFixings = dataSource.Where(e => e.EventType == "FIXING").ToList();
                CollectionAssert.AreEqual(fixings, sameFixings);
            }
        }

        [Test]
        public void Feed_many_sends_all_data()
        {
            // check that empty request are correctly split
            //var empty = new PutRequest("test"){EndOfSession = true, SessionId = Guid.NewGuid()};
            //var split = empty.SplitWithMaxSize();
            //Assert.AreEqual(1,split.Count);
            //Assert.IsTrue(split[0].EndOfSession);
            //Assert.IsEmpty(split[0].Items);


            using var connector = new Connector(_clientConfig);
            connector.AdminInterface().DropDatabase();

            connector.DeclareCollection<Order>();

            var dataSource = connector.DataSource<Order>();


            var orders = Order.GenerateTestData(100_000);

            dataSource.PutMany(orders);

            Assert.AreEqual(100_000, dataSource.Count());
        }

        [Test]
        public void Test_order_by()
        {
            using var connector = new Connector(_clientConfig);
            connector.AdminInterface().DropDatabase();

            connector.DeclareCollection<Order>();

            var dataSource = connector.DataSource<Order>();

            var orders = Order.GenerateTestData(10_000);


            dataSource.PutMany(orders);

            // warm up
            var _ = dataSource.Where(o => o.Category == "geek").ToList();
            _ = dataSource.Where(o => o.Category == "geek").OrderBy(o => o.Amount).ToList();
            _ = dataSource.Where(o => o.Category == "geek").OrderByDescending(o => o.Amount).ToList();

            var watch = new Stopwatch();
            watch.Start();

            var noOrder = dataSource.Where(o => o.Category == "geek").ToList();

            Console.WriteLine(
                $"Getting {noOrder.Count} objects without order-by took {watch.ElapsedMilliseconds} milliseconds");

            watch.Restart();

            var ascending = dataSource.Where(o => o.Category == "geek").OrderBy(o => o.Amount).ToList();

            Console.WriteLine(
                $"Getting {ascending.Count} objects with order-by took {watch.ElapsedMilliseconds} milliseconds");

            Assert.AreEqual(noOrder.Count, ascending.Count);

            watch.Restart();

            var descending = dataSource.Where(o => o.Category == "geek").OrderByDescending(o => o.Amount).ToList();

            Console.WriteLine(
                $"Getting {descending.Count} objects with order-by descending took {watch.ElapsedMilliseconds} milliseconds");

            Assert.AreEqual(noOrder.Count, descending.Count);

            // check that they are ordered

            // check sorted ascending
            for (var i = 0; i < ascending.Count - 1; i++)
                Assert.LessOrEqual((int)ascending[i].Amount * 10000, (int)ascending[i + 1].Amount * 10000);

            // check sorted descending
            for (var i = 0; i < descending.Count - 1; i++)
                Assert.GreaterOrEqual((int)descending[i].Amount * 10000, (int)descending[i + 1].Amount * 10000);

            watch.Stop();
        }

        [Test]
        public void Test_the_sql2json_interface()
        {
            using var connector = new Connector(_clientConfig);
            connector.AdminInterface().DropDatabase();

            connector.DeclareCollection<Home>();

            var dataSource = connector.DataSource<Home>();
            dataSource.Put(new Home
                { Id = 1, CountryCode = "FR", Town = "Paris", Rooms = 1, Bathrooms = 1, PriceInEuros = 254 });
            dataSource.Put(new Home
                { Id = 2, CountryCode = "FR", Town = "Toulouse", Rooms = 2, Bathrooms = 1, PriceInEuros = 256 });
            dataSource.Put(new Home
                { Id = 3, CountryCode = "CA", Town = "Toronto", Rooms = 3, Bathrooms = 2, PriceInEuros = 55.5M });

            var all = connector.SqlQueryAsJson("select from home").ToList();
            Assert.AreEqual(3, all.Count);

            var r1 = connector.SqlQueryAsJson("select from home where countryCode=CA").ToList();
            Assert.AreEqual(1, r1.Count);
            Assert.AreEqual("Toronto", r1[0]["Town"].Value<string>());

            var r2 = connector.SqlQueryAsJson("select from home where rooms in (1, 2)").ToList();
            Assert.AreEqual(2, r2.Count);

            var r3 = connector.SqlQueryAsJson("select from home where rooms not in (1, 2)").ToList();
            Assert.AreEqual(1, r3.Count);


            var r4 = connector.SqlQueryAsJson("select from home where CountryCode not in (FR, 'CA')").ToList();
            Assert.AreEqual(0, r4.Count);

            var r5 = connector.SqlQueryAsJson("select from home where PriceInEuros < 56").ToList();
            Assert.AreEqual(1, r5.Count);
            r5 = connector.SqlQueryAsJson("select from home where PriceInEuros < 56 and PriceInEuros > 55").ToList();
            Assert.AreEqual(1, r5.Count);

            r5 = connector.SqlQueryAsJson("select from home where PriceInEuros <= 56").ToList();
            Assert.AreEqual(1, r5.Count);
        }

        [Test]
        public void Conditional_put()
        {
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Event>();

                connector.DeclareCollection<Event>();

                var events = connector.DataSource<Event>();

                var wasAdded = events.TryAdd(new FixingEvent(1, "AXA", 150, "EQ-256"));

                Assert.IsTrue(wasAdded);

                // the second time it should return false as it is already there
                wasAdded = events.TryAdd(new FixingEvent(1, "AXA", 160, "EQ-256"));

                Assert.IsFalse(wasAdded);

                var reloaded = (FixingEvent)events[1];

                // check that the original value is still there
                Assert.AreEqual(150, reloaded.Value);
            }

            // check also that it has not been saved in the persistence storage
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Event>();
                var events = connector.DataSource<Event>();

                var reloaded = (FixingEvent)events[1];

                // check that the original value is still there
                Assert.AreEqual(150, reloaded.Value);
            }
        }


        [Test]
        public void Conditional_update()
        {
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Event>();

                var events = connector.DataSource<Event>();

                var wasAdded = events.TryAdd(new FixingEvent(1, "AXA", 150, "EQ-256") { Timestamp = DateTime.Now });

                Assert.IsTrue(wasAdded);

                var reloaded = (FixingEvent)events[1];
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
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Event>();

                var events = connector.DataSource<Event>();

                var reloaded = (FixingEvent)events[1];

                // check that the updated value is persistent
                Assert.AreEqual(160, reloaded.Value);
            }
        }

        [Test]
        public void Check_schema_compatibility()
        {
            var schema1 = SchemaFactory.New("order")
                .PrimaryKey("Id")
                .WithServerSideValue("Category", IndexType.Dictionary)
                .WithServerSideValue("Amount")
                .Build();

            var schema2 = SchemaFactory.New("order")
                .PrimaryKey("Id")
                .WithServerSideValue("Category", IndexType.Dictionary)
                .WithServerSideValue("Amount", IndexType.Ordered)
                .Build();

            var compatibility = CollectionSchema.AreCompatible(schema2, schema1);

            Assert.AreEqual(CollectionSchema.CompatibilityLevel.NeedsReindex, compatibility,
                "new index so it should be reindexed");

            compatibility = CollectionSchema.AreCompatible(schema1, schema2);

            Assert.AreEqual(CollectionSchema.CompatibilityLevel.Ok, compatibility, "less indexes so nothing to do");

            compatibility = CollectionSchema.AreCompatible(schema2, schema2);

            Assert.AreEqual(CollectionSchema.CompatibilityLevel.Ok, compatibility, "same schema so nothing to do");

            var schema3 = SchemaFactory.New("order")
                .PrimaryKey("Id")
                .WithServerSideValue("Category", IndexType.Dictionary)
                .WithServerSideValue("Amount", IndexType.Ordered)
                .WithServerSideValue("IsDelivered", IndexType.Dictionary)
                .Build();

            compatibility = CollectionSchema.AreCompatible(schema3, schema1);
            Assert.AreEqual(CollectionSchema.CompatibilityLevel.NeedsRepacking, compatibility,
                "new server-side value added so full repacking is needed");

            compatibility = CollectionSchema.AreCompatible(schema3, schema2);
            Assert.AreEqual(CollectionSchema.CompatibilityLevel.NeedsRepacking, compatibility,
                "new server-side value added so full repacking is needed");
        }

        [Test]
        public void Reindex_existing_collection()
        {
            //store objects with a schema
            using (var connector = new Connector(_clientConfig))
            {
                var description1 = SchemaFactory.New("order")
                    .PrimaryKey("Id")
                    .WithServerSideValue("Category", IndexType.Dictionary)
                    .WithServerSideValue("Amount", IndexType.Ordered)
                    .Build();

                connector.DeclareCollection("orders", description1);

                var orders = connector.DataSource<Order>("orders");

                orders.Put(new Order
                {
                    Id = Guid.NewGuid(),
                    Category = "geek",
                    Amount = 110.3,
                    ProductId = 101,
                    IsDelivered = false
                });

                orders.Put(new Order
                {
                    Id = Guid.NewGuid(),
                    Category = "geek",
                    Amount = 110.3,
                    ProductId = 101,
                    IsDelivered = true
                });

                var found = orders.Where(o => o.Category == "geek").ToList();

                Assert.AreEqual(2, found.Count);

                Assert.Throws<CacheException>(() =>
                {
                    var unused = orders.Where(o => o.IsDelivered).ToList();
                });
            }

            // change the schema to cause object repacking
            using (var connector = new Connector(_clientConfig))
            {
                var description1 = SchemaFactory.New("order")
                    .PrimaryKey("Id")
                    .WithServerSideValue("Category", IndexType.Dictionary)
                    .WithServerSideValue("Amount", IndexType.Ordered)
                    .WithServerSideValue("IsDelivered", IndexType.Dictionary)
                    .Build();

                connector.DeclareCollection("orders", description1);

                var orders = connector.DataSource<Order>("orders");


                var found = orders.Where(o => o.Category == "geek").ToList();

                Assert.AreEqual(2, found.Count);

                // now it should work as collection schema changed. Now it contains IsDelivered index
                var delivered = orders.Where(o => o.IsDelivered).ToList();
                Assert.AreEqual(1, delivered.Count);
            }
        }


        [Test]
        public void Connection_is_restored_when_a_server_restarts()
        {
            using var connector = new Connector(_clientConfig);
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

            RestartOneServer();

            events = dataSource.Where(evt => evt.EventType == "FIXING").ToList();

            Assert.AreEqual(2, events.Count);
        }

        [Test]
        public void Delete_many_and_restart()
        {
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Event>();

                var clusterDescription = connector.GetClusterDescription();

                Assert.AreEqual(_servers.Count, clusterDescription.ServersStatus.Length);

                var dataSource = connector.DataSource<Event>();

                var events = new List<Event>();
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


                // delete one fixing event
                dataSource.DeleteMany(e => e.EventType == "FIXING");

                events = dataSource.Where(evt => evt.EventType == "FIXING").ToList();

                Assert.AreEqual(0, events.Count);
            }

            StopServers();
            StartServers();

            // check that data is available after restart

            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Event>();

                var dataSource = connector.DataSource<Event>();

                var events = dataSource.Where(evt => evt.EventType == "FIXING").ToList();

                Assert.AreEqual(0, events.Count);
            }
        }


        [Test]
        public void Dump_and_import_dump_with_multiple_servers()
        {
            var dumpPath = "dump";

            if (Directory.Exists(dumpPath))
                Directory.Delete(dumpPath, true);

            Directory.CreateDirectory(dumpPath);

            int maxId1;
            int maxId2;
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Event>();

                var dataSource = connector.DataSource<Event>();

                var events = new List<Event>();
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
                Assert.IsTrue(fixings > 50, "fixings > 5000");


                // generate unique ids before dump
                maxId1 = connector.GenerateUniqueIds("blahblah", 20).Max();
                maxId2 = connector.GenerateUniqueIds("foobar", 20).Max();

                var admin = connector.AdminInterface();
                admin.Dump(dumpPath);

                // check that dumping did not affect existing data
                fixings = dataSource.Count(e => e.EventType == "FIXING");
                Assert.IsTrue(fixings > 50, "fixings > 5000");

                dataSource.Put(new FixingEvent(55555, "GLE", 180, "IRD-500"));
            }

            StopServers();
            StartServers();

            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Event>();

                var dataSource = connector.DataSource<Event>();

                var eventAfterDump = dataSource[55555];
                Assert.IsNotNull(eventAfterDump);

                var admin = connector.AdminInterface();
                admin.ImportDump(dumpPath);


                // generate unique ids after dump and check that they are higher than the one generated before dump
                // meaning the unique id generators (sequences)  have been restored
                var minId1 = connector.GenerateUniqueIds("blahblah", 20).Max();
                var minId2 = connector.GenerateUniqueIds("foobar", 20).Max();

                Assert.Greater(minId1, maxId1, "the sequences ware not correctly restored from dump");
                Assert.Greater(minId2, maxId2, "the sequences ware not correctly restored from dump");

                eventAfterDump = dataSource[55555];
                // now it should be null as it was added after dump and we re imported the dump
                Assert.IsNull(eventAfterDump);

                var fixings = dataSource.Count(e => e.EventType == "FIXING");

                Assert.IsTrue(fixings > 50, "fixings > 5000");
            }
        }


        [Test]
        public void Dump_and_import_compressed_data_with_multiple_servers()
        {
            var dumpPath = "dump";

            if (Directory.Exists(dumpPath))
                Directory.Delete(dumpPath, true);

            Directory.CreateDirectory(dumpPath);

            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<CompressedItem>();
                var dataSource = connector.DataSource<CompressedItem>();

                var items = new List<CompressedItem>();
                for (var i = 0; i < 100; i++) items.Add(new CompressedItem { Id = i });

                dataSource.PutMany(items);


                var admin = connector.AdminInterface();
                admin.Dump(dumpPath);


                dataSource.Put(new CompressedItem { Id = 133 });
            }

            StopServers();
            StartServers();

            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<CompressedItem>();

                var dataSource = connector.DataSource<CompressedItem>();

                var afterDump = dataSource[133];
                Assert.IsNotNull(afterDump);

                var admin = connector.AdminInterface();
                admin.ImportDump(dumpPath);

                // this time it should be null as it was added after the backup and backup was restored
                afterDump = dataSource[133];
                Assert.IsNull(afterDump);

                var after = dataSource.Count();
                Assert.AreEqual(100, after);
            }
        }

        [Test]
        public void Dump_and_import_with_server_side_values()
        {
            var dumpPath = "dump";

            if (Directory.Exists(dumpPath))
                Directory.Delete(dumpPath, true);

            Directory.CreateDirectory(dumpPath);


            decimal sum1;

            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Order>();

                var dataSource = connector.DataSource<Order>();

                var orders = new List<Order>();

                // generate orders for three categories
                for (var i = 0; i < 100; i++)
                {
                    var order = new Order
                    {
                        Id = Guid.NewGuid(),
                        Amount = 10.15,
                        ClientId = 100 + i + 10,
                        Date = DateTimeOffset.Now,
                        Category = "geek",
                        ProductId = 1000 + i % 10,
                        Quantity = 2
                    };

                    if (i % 5 == 0)
                        order.Category = "sf";
                    else if (i % 5 == 1) order.Category = "science";

                    orders.Add(order);
                }

                dataSource.PutMany(orders);

                var pivot = dataSource.PreparePivotRequest().OnAxis(o => o.ClientId)
                    .AggregateValues(o => o.Amount, o => o.Quantity).Execute();

                sum1 = pivot.AggregatedValues.Single(v => v.ColumnName == "Amount").Sum;

                var admin = connector.AdminInterface();
                admin.Dump(dumpPath);


                dataSource.Put(new Order
                {
                    Id = Guid.NewGuid(),
                    Amount = 10.15,
                    ClientId = 2,
                    Date = DateTimeOffset.Now,
                    Category = "youpee",
                    ProductId = 5,
                    Quantity = 2
                });
            }

            StopServers();
            StartServers();

            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Order>();

                var dataSource = connector.DataSource<Order>();

                var afterDump = dataSource.Where(o => o.ClientId == 2).ToList();
                Assert.True(afterDump.Count == 1);

                var admin = connector.AdminInterface();
                admin.ImportDump(dumpPath);

                // this time it should not be found as it was added after the backup and backup was restored
                afterDump = dataSource.Where(o => o.ClientId == 2).ToList();
                Assert.True(afterDump.Count == 0);

                var after = dataSource.Count();
                Assert.AreEqual(100, after);

                // the pivot should be identical 
                var pivot = dataSource.PreparePivotRequest().OnAxis(o => o.ClientId)
                    .AggregateValues(o => o.Amount, o => o.Quantity).Execute();
                var sum2 = pivot.AggregatedValues.Single(v => v.ColumnName == "Amount").Sum;

                Assert.AreEqual(sum1, sum2);
            }
        }

        [Test]
        public void Dump_and_init_from_dump_changing_the_number_of_nodes()
        {
            var dumpPath = "dump";

            if (Directory.Exists(dumpPath))
                Directory.Delete(dumpPath, true);

            Directory.CreateDirectory(dumpPath);

            int count;

            int maxId1;
            int maxId2;

            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Event>();

                var dataSource = connector.DataSource<Event>();

                var events = new List<Event>();
                for (var i = 0; i < 110; i++)
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


                // generate unique ids on two generators
                var ids = connector.GenerateUniqueIds("one", 10);
                Assert.AreEqual(10, ids.Length);
                CollectionAssert.AllItemsAreUnique(ids, "identifiers are not unique");
                maxId1 = ids.Max();

                ids = connector.GenerateUniqueIds("two", 19);
                Assert.AreEqual(19, ids.Length);
                CollectionAssert.AllItemsAreUnique(ids, "identifiers are not unique");
                maxId2 = ids.Max();

                count = dataSource.Count(e => e.EventType == "INCREASE");


                Assert.IsTrue(count > 30);

                // check that empty result is managed
                var zero = dataSource.Count(e => e.EventId == 3 && e.DealId == "none");
                Assert.AreEqual(0, zero);

                var empty = dataSource.Where(e => e.EventId == 3 && e.DealId == "none").ToList();
                Assert.IsEmpty(empty);

                var admin = connector.AdminInterface();
                admin.Dump(dumpPath);
            }

            StopServers();

            // delete the data
            for (var i = 0; i < ServerCount + 1; i++)
                if (Directory.Exists($"server{i:D2}"))
                    Directory.Delete($"server{i:D2}", true);

            // add one server to the cluster
            StartServers(ServerCount + 1);

            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Event>();

                var admin = connector.AdminInterface();


                admin.InitializeFromDump(dumpPath);

                var dataSource = connector.DataSource<Event>();

                var countAfter = dataSource.Count(e => e.EventType == "INCREASE");

                Assert.AreEqual(count, countAfter);


                // new unique ids are bigger than the ones generated before dumps (sequences continue at the previous max value after dump import)
                var ids = connector.GenerateUniqueIds("one", 10);
                Assert.AreEqual(10, ids.Length);
                CollectionAssert.AllItemsAreUnique(ids, "identifiers are not unique");
                var minId1 = ids.Min();

                ids = connector.GenerateUniqueIds("two", 19);
                Assert.AreEqual(19, ids.Length);
                CollectionAssert.AllItemsAreUnique(ids, "identifiers are not unique");
                var minId2 = ids.Max();

                Assert.IsTrue(minId1 > maxId1, "the sequences were not resynchronized after reinitializing from dump");
                Assert.IsTrue(minId2 > maxId2, "the sequences were not resynchronized after reinitializing from dump");
            }

            // restart and check that the query gives the same result
            StopServers();

            // add one server to the cluster
            StartServers(ServerCount + 1);

            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Event>();

                var dataSource = connector.DataSource<Event>();

                var countAfter = dataSource.Count(e => e.EventType == "INCREASE");

                Assert.AreEqual(count, countAfter);
            }
        }


        [Test]
        public void Full_text_search()
        {
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Home>();

                var dataSource = connector.DataSource<Home>();

                dataSource.PutMany(new[]
                {
                    new Home
                    {
                        Id = 10, Address = "14 rue de le pompe", Town = "Paris", Comments = new List<Comment>
                        {
                            new Comment { Text = "close to the metro" },
                            new Comment { Text = "beautiful view" }
                        }
                    },

                    new Home
                    {
                        Id = 20, Address = "10 rue du chien qui fume", Town = "Nice", Comments = new List<Comment>
                        {
                            new Comment { Text = "close to the metro" },
                            new Comment { Text = "ps4" }
                        }
                    }
                });


                var result1 = dataSource.FullTextSearch("rue de la pompe").ToList();
                Assert.AreEqual(1, result1.Count);
                Assert.AreEqual(10, result1.First().Id);

                var result2 = dataSource.FullTextSearch("close metro").ToList();
                Assert.AreEqual(2, result2.Count);

                result2 = dataSource.FullTextSearch("close metro").Take(1).ToList();
                Assert.AreEqual(1, result2.Count);

                var result3 = dataSource.FullTextSearch("close metro ps4").ToList();
                Assert.AreEqual(2, result3.Count);
                Assert.AreEqual(20, result3.First().Id, "the best match was not returned first");

                result3 = dataSource.FullTextSearch("close metro ps").ToList();
                Assert.AreEqual(2, result3.Count);
                Assert.AreEqual(20, result3.First().Id, "the best match was not the first returned");

                var result4 = dataSource.FullTextSearch("blah blah paris").ToList();
                Assert.AreEqual(10, result4.First().Id);

                //  this last one should be found by pure "same document" strategy
                result3 = dataSource.FullTextSearch("metro ps").ToList();
                Assert.AreEqual(20, result3.First().Id, "the best match was not the first returned");

                // search single token
                result3 = dataSource.FullTextSearch("ps").ToList();
                Assert.AreEqual(1, result3.Count);
                Assert.AreEqual(20, result3.Single().Id, "only one object should be returned");

                // search unknown token
                var result5 = dataSource.FullTextSearch("blah").ToList();
                Assert.AreEqual(0, result5.Count);
            }

            StopServers();
            StartServers();

            // check that full text search still works after restart

            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Home>();

                var homes = connector.DataSource<Home>();

                var result1 = homes.FullTextSearch("rue de la pompe").ToList();
                Assert.AreEqual(10, result1.First().Id);

                var updated = new Home
                {
                    Id = 20,
                    Address = "10 rue du chien qui fume",
                    Town = "Nice",
                    Comments = new List<Comment>
                    {
                        new Comment { Text = "close to the metro" },
                        new Comment { Text = "4k tv" }
                    }
                };

                homes.Put(updated);

                // as the object was updated this query will return no result
                var result = homes.FullTextSearch("ps").ToList();
                Assert.AreEqual(0, result.Count);

                result = homes.FullTextSearch("4k").ToList();
                Assert.AreEqual(1, result.Count);
                Assert.AreEqual(20, result.Single().Id, "newly updated object not found");

                // now delete the object. The full-text search should not return the previous result any more
                homes.Delete(updated);
                result = homes.FullTextSearch("4k").ToList();
                Assert.AreEqual(0, result.Count);
            }
        }


        [Test]
        public void Generate_unique_ids_with_multiple_nodes()
        {
            int max;
            using (var connector = new Connector(_clientConfig))
            {
                var ids = connector.GenerateUniqueIds("test", 13);

                Assert.AreEqual(13, ids.Length);

                CollectionAssert.AllItemsAreUnique(ids, "identifiers are not unique");

                max = ids.Max();

                Assert.IsTrue(ids.Min() > 0, "unique ids should be strictly positive");
            }


            // check that after restart unique ids are bigger than the previous ones
            using (var connector = new Connector(_clientConfig))
            {
                var ids = connector.GenerateUniqueIds("test", 13);

                Assert.AreEqual(13, ids.Length);

                CollectionAssert.AllItemsAreUnique(ids, "identifiers are not unique");


                Assert.IsTrue(ids.Min() > max, "sequence persistence failure");

                max = ids.Max();
            }

            // ask for less than the number of nodes (10)
            using (var connector = new Connector(_clientConfig))
            {
                var ids = connector.GenerateUniqueIds("test", 2);

                Assert.AreEqual(2, ids.Length);

                CollectionAssert.AllItemsAreUnique(ids, "identifiers are not unique");


                Assert.IsTrue(ids.Min() > max, "sequence persistence failure");
            }
        }


        [Test]
        public void Generate_unique_ids_with_multiple_threads()
        {
            var all = new HashSet<int>();
            var rand = new Random(Environment.TickCount);

            using var connector = new Connector(_clientConfig);
            Parallel.For(0, 1000, i =>
            {
                // ReSharper disable once AccessToDisposedClosure
                var ids = connector.GenerateUniqueIds("test", rand.Next(100) + 1);

                lock (all)
                {
                    foreach (var id in ids)
                    {
                        var notAlreadyThere = all.Add(id);
                        Assert.IsTrue(notAlreadyThere);
                    }
                }
            });
        }


        [Test]
        public void Generated_ids_are_uniformly_distributed()
        {
            using var connector = new Connector(_clientConfig);
            var objectsPerNode = new int[ServerCount];

            for (var i = 0; i < 1000; i++)
            {
                var id = connector.GenerateUniqueIds("xxx", 1)[0];
                var node = id % ServerCount;
                objectsPerNode[node]++;
            }

            Assert.IsTrue(objectsPerNode.All(o => o > 0));
        }

        [Test]
        public void Import_real_data_set()
        {
            var schema = TypedSchemaFactory.FromType(typeof(Business));

            var serializer = new JsonSerializer();

            var businesses = serializer.Deserialize<List<Business>>(
                new JsonTextReader(new StreamReader(new FileStream("TestData/yelp.json", FileMode.Open))));

            Assert.IsTrue(businesses?.Count > 0);

            PackedObject.Pack(businesses[0], schema);

            var comments = businesses.SelectMany(b => b.Reviews).ToList();

            Assert.IsTrue(comments.Any(c => c.Text.Contains("Musashi")));


            using var connector = new Connector(_clientConfig);

            connector.DeclareCollection<Business>();

            var data = connector.DataSource<Business>();

            data.PutMany(businesses);

            var result = data.FullTextSearch("Musashi").ToList();
            Assert.IsTrue(result.Any());


            result = data.FullTextSearch("enjoyable evening").ToList();
            Assert.IsTrue(result.Count >= 1);
            Assert.IsTrue(result[0].Reviews.Any(r => r.Text.Contains("enjoyable evening")),
                "the first result should contain the exact expression");

            result = data.FullTextSearch("panera").ToList();
            Assert.AreEqual(1, result.Count);
        }

#if DEBUG
        // this test can work only in debug environment as failure simulations are deactivated in release
        [Test]
        public void In_case_of_failure_during_dump_import_data_is_rolled_back()
        {
            var dumpPath = "dump";

            if (Directory.Exists(dumpPath))
                Directory.Delete(dumpPath, true);

            Directory.CreateDirectory(dumpPath);


            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Event>();

                var dataSource = connector.DataSource<Event>();

                var events = new List<Event>();
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

                var admin = connector.AdminInterface();
                admin.Dump(dumpPath);

                dataSource.Put(new FixingEvent(55555, "GLE", 180, "IRD-500"));
            }

            StopServers();
            StartServers();

            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Event>();

                var dataSource = connector.DataSource<Event>();

                var eventAfterDump = dataSource[55555];
                Assert.IsNotNull(eventAfterDump);

                // simulate a failure on the 3rd node
                Dbg.ActivateSimulation(100, 3);

                var admin = connector.AdminInterface();


                try
                {
                    admin.ImportDump(dumpPath);
                    Assert.Fail("An exception was expected here");
                }
                catch (CacheException e)
                {
                    Assert.IsTrue(e.Message.ToLower().Contains("simulation"));
                }


                eventAfterDump = dataSource[55555];
                // this event was added after dump and it's still present as the dump was rolled-back
                Assert.NotNull(eventAfterDump);


                // check that it is still working fine after rollback
                dataSource.Put(new FixingEvent(66666, "GLE", 180, "IRD-500"));

                var events = new[] { 55555, 66666 };

                var evts = dataSource.Where(e => events.Contains(e.EventId)).ToList();

                Assert.AreEqual(2, evts.Count);
            }


            StopServers();
            StartServers();


            // check that everything is persisted
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Event>();

                var dataSource = connector.DataSource<Event>();

                var events = new[] { 55555, 66666 };
                var evts = dataSource.Where(e => events.Contains(e.EventId)).ToList();

                Assert.AreEqual(2, evts.Count);
            }
        }
#endif

        [Test]
        public void Mixed_search()
        {
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Home>();
                var dataSource = connector.DataSource<Home>();

                dataSource.PutMany(new[]
                {
                    new Home
                    {
                        Id = 10, Address = "14 rue de le pompe", Town = "Paris", CountryCode = "FR", Comments =
                            new List<Comment>
                            {
                                new Comment { Text = "close to the metro" },
                                new Comment { Text = "beautiful view" }
                            }
                    },

                    new Home
                    {
                        Id = 20, Address = "10 rue du chien qui fume", Town = "Nice", CountryCode = "FR", Comments =
                            new List<Comment>
                            {
                                new Comment { Text = "close to the metro" },
                                new Comment { Text = "ps4" }
                            }
                    }
                });

                var result = dataSource.Where(h => h.Town == "Paris").FullTextSearch("close metro").ToList();
                Assert.AreEqual(1, result.Count);

                var result1 = dataSource.Where(h => h.CountryCode == "FR").FullTextSearch("close metro").ToList();
                Assert.AreEqual(2, result1.Count);

                var result3 = dataSource.Where(h => h.CountryCode == "FR").FullTextSearch("ps4").ToList();
                Assert.AreEqual(1, result3.Count);

                var result4 = dataSource.Where(h => h.CountryCode == "FR").FullTextSearch("close metro ps").ToList();
                Assert.AreEqual(2, result4.Count);
                Assert.AreEqual(20, result4.First().Id, "should be ordered by the full-text score");
            }
        }

        [Test]
        public void Optimistic_synchronization_with_timestamp()
        {
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Event>();

                var events = connector.DataSource<Event>();

                var wasAdded = events.TryAdd(new FixingEvent(1, "AXA", 150, "EQ-256") { Timestamp = DateTime.Now });

                Assert.IsTrue(wasAdded);

                var reloaded = (FixingEvent)events[1];
                var firstVersion = (FixingEvent)events[1];


                reloaded.Value = 160;

                // first one should work
                events.UpdateWithTimestampSynchronization(reloaded);

                Assert.AreNotEqual(reloaded.Timestamp, firstVersion.Timestamp,
                    "Timestamp should have been updated automatically");


                firstVersion.Value = 111;

                // second one should fail as the object has already been modified
                Assert.Throws<CacheException>(() => events.UpdateWithTimestampSynchronization(firstVersion));
            }

            // check also that it has not been saved in the persistence storage
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Event>();

                var events = connector.DataSource<Event>();

                var reloaded = (FixingEvent)events[1];

                // check that the updated value is persistent
                Assert.AreEqual(160, reloaded.Value);
            }
        }


        [Test]
        public void Some_data_manipulation_with_multiple_servers()
        {
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Event>();

                var dataSource = connector.DataSource<Event>();

                dataSource.PutMany(new Event[]
                {
                    new FixingEvent(1, "AXA", 150, "EQ-256"),
                    new FixingEvent(2, "TOTAL", 180, "IRD-400"),
                    new Increase(3, 180, "EQ-256")
                });


                var allEvents = dataSource.ToList().OrderBy(e => e.EventId).ToList();

                var events = dataSource.Where(evt => evt.DealId == "EQ-256").ToList();

                Assert.AreEqual(2, events.Count);

                events = dataSource.Where(evt => evt.EventType == "FIXING").ToList();

                Assert.AreEqual(2, events.Count);


                // delete one fixing event
                dataSource.Delete(allEvents[0]);

                events = dataSource.Where(evt => evt.EventType == "FIXING").ToList();

                Assert.AreEqual(1, events.Count);
            }

            StopServers();
            StartServers();

            // check that data is available after restart

            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Event>();

                var dataSource = connector.DataSource<Event>();

                var events = dataSource.Where(evt => evt.EventType == "FIXING").ToList();

                Assert.AreEqual(1, events.Count);

                events = dataSource.Where(evt => evt.DealId == "EQ-256").ToList();

                Assert.AreEqual(1, events.Count);

                connector.AdminInterface().ReadOnlyMode();

                Assert.Throws<CacheException>(() => dataSource.Put(new FixingEvent(1, "AXA", 150, "EQ-256")));


                // switch back to read-write mode and now it should work
                connector.AdminInterface().ReadOnlyMode(true);

                dataSource.Put(new FixingEvent(1, "AXA", 150, "EQ-256"));
            }
        }


        [Test]
        public void Take_and_skip_extension_methods()
        {
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Event>();

                var dataSource = connector.DataSource<Event>();

                var events = new List<Event>();
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
        public void Select_only_some_properties()
        {
            using var connector = new Connector(_clientConfig);

            connector.DeclareCollection<Order>();

            var dataSource = connector.DataSource<Order>();

            dataSource.Put(new Order { Category = "geek", ClientId = 101 });


            // select more than one property with aliases (like select Category Cat, ClientId from ...)
            var result1 = dataSource.Select(o => new { Cat = o.Category, o.ClientId }).ToList();

            Assert.AreEqual(1, result1.Count);
            Assert.AreEqual("geek", result1[0].Cat);
            Assert.AreEqual(101, result1[0].ClientId);

            // select primitive types
            var result2 = dataSource.Select(o => o.Category).ToList();
            Assert.AreEqual(1, result2.Count);
            Assert.AreEqual("geek", result2[0]);

            var result3 = dataSource.Select(o => o.ClientId).ToList();
            Assert.AreEqual(1, result3.Count);
            Assert.AreEqual(101, result3[0]);

            // select all with take

            var result4 = dataSource.Take(1).ToList();
            Assert.AreEqual(1, result4.Count);
        }

        [Test]
        public void Select_with_distinct_clause()
        {
            using var connector = new Connector(_clientConfig);

            connector.DeclareCollection<Order>();

            var dataSource = connector.DataSource<Order>();

            dataSource.Put(new Order { Category = "geek", ClientId = 101 });
            dataSource.Put(new Order { Category = "geek", ClientId = 101 });
            dataSource.Put(new Order { Category = "geek", ClientId = 102 });


            // select more than one property with aliases (like select Category Cat, ClientId from ...)
            var result1 = dataSource.Select(o => new { Cat = o.Category, o.ClientId }).Distinct().ToList();

            Assert.AreEqual(2, result1.Count);
            Assert.AreEqual("geek", result1[0].Cat);
            Assert.AreEqual(1, result1.Count(r => r.ClientId == 102));
            Assert.AreEqual(1, result1.Count(r => r.ClientId == 101));


            // select primitive types
            var result2 = dataSource.Select(o => o.Category).Distinct().ToList();
            Assert.AreEqual(1, result2.Count);
            Assert.AreEqual("geek", result2[0]);

            var result3 = dataSource.Select(o => o.ClientId).Distinct().ToList();
            Assert.AreEqual(2, result3.Count);
        }


        [Test]
        public void More_select_and_distinct()
        {
            using var connector = new Connector(_clientConfig);

            connector.DeclareCollection<Order>();

            var dataSource = connector.DataSource<Order>();

            dataSource.Put(new Order { Category = "geek", ClientId = 101, IsDelivered = true });
            dataSource.Put(new Order { Category = "geek", ClientId = 101 });
            dataSource.Put(new Order { Category = "geek", ClientId = 102, IsDelivered = true });
            dataSource.Put(new Order { Category = "sf", ClientId = 102 });


            var result1 = dataSource.Where(o => !o.IsDelivered).Select(o => new { o.Category, o.ClientId }).Distinct()
                .ToList();
            Assert.AreEqual(2, result1.Count);

            var result2 = dataSource.Where(o => o.ClientId == 102).Select(o => o.Category).Distinct().ToList();
            Assert.AreEqual(2, result2.Count);


            var result3 = dataSource.Where(o => o.IsDelivered).Select(o => o.Category).Distinct().ToList();
            Assert.AreEqual(1, result3.Count);

            var result4 = dataSource.Where(o => o.Category == "geek").Select(o => o.IsDelivered).Distinct().ToList();
            Assert.AreEqual(2, result4.Count);


            dataSource.Put(new Order { Category = "sf", ClientId = 103 });
            dataSource.Put(new Order { Category = "sf", ClientId = 104 });
            dataSource.Put(new Order { Category = "travel", ClientId = 105 });

            var cats = new[] { "sf", "travel" };
            var result5 = dataSource.Where(o => cats.Contains(o.Category)).Select(o => o.ClientId).Distinct().ToList();
            Assert.AreEqual(4, result5.Count);

            var result6 = dataSource.Where(o => o.Category == "sf" || o.Category == "travel").Select(o => o.ClientId)
                .Distinct().ToList();
            CollectionAssert.AreEqual(result5, result6);

            // with precompiled queries
            var categories = new[] { "sf", "travel" };

            var resultWithLinq = dataSource.Where(o => categories.Contains(o.Category)).ToList();

            var query = dataSource.PredicateToQuery(o => categories.Contains(o.Category));
            var resultWithPrecompiled = dataSource.WithPrecompiledQuery(query).ToList();

            Assert.AreEqual(resultWithLinq.Count, resultWithPrecompiled.Count);
        }

        [Test]
        public void Distinct_for_single_property()
        {
            using var connector = new Connector(_clientConfig);

            connector.DeclareCollection<AllKindsOfProperties>();

            var collection = connector.DataSource<AllKindsOfProperties>();

            collection.Put(new AllKindsOfProperties
                { InstrumentName = "instr01", Tags = new List<string> { "a", "b", "c" } });
            collection.Put(new AllKindsOfProperties
                { InstrumentName = "instr02", Tags = new List<string> { "x", "y", "c" } });
            collection.Put(new AllKindsOfProperties
                { InstrumentName = "instr02", Tags = new List<string> { "x", "y", "z" } });

            // scalar property (indexed)
            var ji = connector.SqlQueryAsJson("select distinct InstrumentName from AllKindsOfProperties").ToList();
            Assert.AreEqual(2, ji.Count);

            var instruments = collection.Select(x => x.InstrumentName).Distinct().ToList();
            Assert.AreEqual(2, instruments.Count);

            // collection property (indexed)
            var jt = connector.SqlQueryAsJson("select distinct tags from AllKindsOfProperties").ToList();
            Assert.AreEqual(6, jt.Count);
        }


        [Test]
        public void Update_items_with_put_many()
        {
            using var connector = new Connector(_clientConfig);

            connector.DeclareCollection<Order>();

            var dataSource = connector.DataSource<Order>();

            var testData = Order.GenerateTestData(10_000);

            dataSource.PutMany(testData);

            var reloaded0 = dataSource.Where(o => o.Category == "sf").ToList();

            reloaded0[0].Category = "vibes";

            dataSource.PutMany(reloaded0.Take(10)); // update less than the bulk insert threshold

            var reloaded1 = dataSource.Where(o => o.Category == "vibes").ToList();

            Assert.AreEqual(1, reloaded1.Count);

            var reloaded2 = dataSource.Where(o => o.Category == "sf").ToList();

            Assert.AreEqual(reloaded0.Count - 1, reloaded2.Count);

            reloaded0[1].Category = "vibes";
            dataSource.PutMany(reloaded0.Take(600)); // update more than the bulk insert threshold

            var reloaded3 = dataSource.Where(o => o.Category == "sf").ToList();

            Assert.AreEqual(reloaded0.Count - 2, reloaded3.Count);
        }

        [Test]
        public void Update_items_with_put()
        {
            using var connector = new Connector(_clientConfig);

            connector.DeclareCollection<Order>();

            var dataSource = connector.DataSource<Order>();

            var testData = Order.GenerateTestData(10_000);

            dataSource.PutMany(testData);

            var reloaded0 = dataSource.Where(o => o.Category == "sf").ToList();

            reloaded0[0].Category = "vibes";

            dataSource.Put(reloaded0[0]);

            var reloaded1 = dataSource.Where(o => o.Category == "vibes").ToList();

            Assert.AreEqual(1, reloaded1.Count);

            var reloaded2 = dataSource.Where(o => o.Category == "sf").ToList();

            Assert.AreEqual(reloaded0.Count - 1, reloaded2.Count);
        }


        [Test]
        public void Sql_queries()
        {
            using var connector = new Connector(_clientConfig);

            connector.DeclareCollection<Order>();

            var dataSource = connector.DataSource<Order>();

            var testData = Order.GenerateTestData(10_000);

            dataSource.PutMany(testData);

            var r1 = dataSource.SqlQuery("select from order").ToList();
            Assert.AreEqual(10_000, r1.Count);

            var r2 = dataSource.SqlQuery("select from order take 10").ToList();
            Assert.AreEqual(10, r2.Count);

            // querying an unknown collection should throw an exception
            Assert.Throws<CacheException>(() => dataSource.SqlQuery("select from no_table take 10").ToList());

            var r3 = dataSource.SqlQuery("select distinct Category from order").ToList();
            Assert.AreEqual(5, r3.Count);
        }

        [Test]
        public void Query_the_activity_table()
        {
            using var connector = new Connector(_clientConfig);

            connector.DeclareCollection<Order>();


            var activity = connector.ActivityLog;

            var dataSource = connector.DataSource<Order>();

            dataSource.Put(new Order { Category = "geek", ClientId = 101, IsDelivered = true });
            dataSource.Put(new Order { Category = "geek", ClientId = 101 });
            dataSource.Put(new Order { Category = "geek", ClientId = 102, IsDelivered = true });
            dataSource.Put(new Order { Category = "sf", ClientId = 102 });


            var result1 = dataSource.Where(o => !o.IsDelivered).Select(o => new { o.Category, o.ClientId }).Distinct()
                .ToList();
            Assert.AreEqual(2, result1.Count);

            //The activity table is filled asynchronously so we need to wait

            Thread.Sleep(2000);
            var logEntries = activity.Where(l => l.Type == "QUERY").ToList();
            Assert.IsTrue(logEntries.All(e =>
                e.ExecutionTimeInMicroseconds == e.ExecutionPlan.TotalTimeInMicroseconds));


            var result2 = dataSource.Where(o => o.ClientId == 102).Select(o => o.Category).Distinct().ToList();
            Assert.AreEqual(2, result2.Count);


            var result3 = dataSource.Where(o => o.IsDelivered).Select(o => o.Category).Distinct().ToList();
            Assert.AreEqual(1, result3.Count);

            var result4 = dataSource.Where(o => o.Category == "geek").Select(o => o.IsDelivered).Distinct().ToList();
            Assert.AreEqual(2, result4.Count);


            dataSource.Put(new Order { Category = "sf", ClientId = 103 });
            dataSource.Put(new Order { Category = "sf", ClientId = 104 });
            dataSource.Put(new Order { Category = "travel", ClientId = 105 });

            var cats = new[] { "sf", "travel" };
            var result5 = dataSource.Where(o => cats.Contains(o.Category)).Select(o => o.ClientId).Distinct().ToList();
            Assert.AreEqual(4, result5.Count);

            var result6 = dataSource.Where(o => o.Category == "sf" || o.Category == "travel").Select(o => o.ClientId)
                .Distinct().ToList();
            CollectionAssert.AreEqual(result5, result6);
        }


        [Test]
        public void Drop_collection()
        {
            using var connector = new Connector(_clientConfig);

            connector.DeclareCollection<Order>();

            
            var dataSource = connector.DataSource<Order>();

            dataSource.Put(new Order { Category = "geek", ClientId = 101, IsDelivered = true });
            dataSource.Put(new Order { Category = "geek", ClientId = 101 });
            dataSource.Put(new Order { Category = "geek", ClientId = 102, IsDelivered = true });
            dataSource.Put(new Order { Category = "sf", ClientId = 102 });

            var schema = connector.GetCollectionSchema("order");

            Assert.IsNotNull(schema);
            Assert.True(schema.ServerSide.Count > 0);

            var desc = connector.GetClusterDescription();
            var userCollections= desc.CollectionsSummary.Count(x => x.Name != "@ACTIVITY");
            Assert.AreEqual(1, userCollections);

            connector.DropCollection("order");

            schema = connector.GetCollectionSchema("order");

            Assert.IsNull(schema);
            
            desc = connector.GetClusterDescription();
            userCollections= desc.CollectionsSummary.Count(x => x.Name != "@ACTIVITY");
            Assert.AreEqual(0, userCollections);


        }
    }
}