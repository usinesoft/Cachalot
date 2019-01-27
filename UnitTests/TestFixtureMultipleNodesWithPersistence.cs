using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cachalot.Linq;
using Channel;
using Client;
using Client.Interface;
using NUnit.Framework;
using UnitTests.TestData.Events;
using ServerConfig = Server.ServerConfig;

namespace UnitTests
{
    [TestFixture]
    public class TestFixtureMultipleNodesWithPersistence
    {
        private class ServerInfo
        {
            public TcpServerChannel Channel { get; set; }
            public Server.Server Server { get; set; }
            public int Port { get; set; }
        }

        private List<ServerInfo> _servers = new List<ServerInfo>();

        private const int ServerCount = 10;

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

        private void StopServers()
        {
            foreach (var serverInfo in _servers)
            {
                serverInfo.Channel.Stop();
                serverInfo.Server.Stop();
            }
        }


        private void RestartOneServer()
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


        private ClientConfig _clientConfig;

        [SetUp]
        public void Init()
        {
            for (var i = 0; i < ServerCount; i++)
                if (Directory.Exists($"server{i:D2}"))
                    Directory.Delete($"server{i:D2}", true);


            StartServers();
        }

        private void StartServers(int serverCount = 0)
        {
            _clientConfig = new ClientConfig();
            _servers = new List<ServerInfo>();

            serverCount = serverCount == 0 ? ServerCount : serverCount;

            for (var i = 0; i < serverCount; i++)
            {
                var serverInfo = new ServerInfo {Channel = new TcpServerChannel()};
                serverInfo.Server =
                    new Server.Server(new ServerConfig(), true, $"server{i:D2}") {Channel = serverInfo.Channel};
                serverInfo.Port = serverInfo.Channel.Init();
                serverInfo.Channel.Start();
                serverInfo.Server.Start();

                _servers.Add(serverInfo);

                _clientConfig.Servers.Add(
                    new Client.Interface.ServerConfig {Host = "localhost", Port = serverInfo.Port});
            }


            Thread.Sleep(500); //be sure the server nodes are started
        }


        [Test]
        public void Connection_is_restored_when_a_server_restarts()
        {
            using (var connector = new Connector(_clientConfig))
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

                RestartOneServer();

                events = dataSource.Where(evt => evt.EventType == "FIXING").ToList();

                Assert.AreEqual(2, events.Count);

            }

        }


        [Test]
        public void Some_data_manipulation_with_multiple_servers()
        {
            using (var connector = new Connector(_clientConfig))
            {
                var dataSource = connector.DataSource<ProductEvent>();

                dataSource.PutMany(new ProductEvent[]
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
                var dataSource = connector.DataSource<ProductEvent>();

                var events = dataSource.Where(evt => evt.EventType == "FIXING").ToList();

                Assert.AreEqual(1, events.Count);

                events = dataSource.Where(evt => evt.DealId == "EQ-256").ToList();

                Assert.AreEqual(1, events.Count);

                connector.AdminInterface().ReadOnlyMode();

                Assert.Throws<CacheException>(() => dataSource.Put(new FixingEvent(1, "AXA", 150, "EQ-256")));



                // switch back to reaswrite mode and now it should work
                connector.AdminInterface().ReadOnlyMode(true);

                dataSource.Put(new FixingEvent(1, "AXA", 150, "EQ-256"));
            }
        }

        [Test]
        public void Delete_many_and_restart()
        {
            using (var connector = new Connector(_clientConfig))
            {

                var clusterDescription = connector.GetClusterDescription();

                Assert.AreEqual(_servers.Count, clusterDescription.ServersStatus.Length);

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
                var dataSource = connector.DataSource<ProductEvent>();

                var events = dataSource.Where(evt => evt.EventType == "FIXING").ToList();

                Assert.AreEqual(0, events.Count);

            }
        }


        [Test]
        public void Take_and_skip_extension_methods()
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
        public void Generated_ids_are_uniformelly_distributed()
        {
            using (var connector = new Connector(_clientConfig))
            {
                var objectsPerNode = new int[ServerCount];

                for (int i = 0; i < 1000; i++)
                {
                    var id = connector.GenerateUniqueIds("xxx", 1)[0];
                    var node = id % ServerCount;
                    objectsPerNode[node]++;
                }

                Assert.IsTrue(objectsPerNode.All(o => o > 0));

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
                var dataSource = connector.DataSource<ProductEvent>();

                var eventAfterDump = dataSource[55555];
                Assert.IsNotNull(eventAfterDump);

                var admin = connector.AdminInterface();
                admin.ImportDump(dumpPath);



                // generate unique ids after dump and check that they are higher than the one generated before dump
                // meanig the unique id generators (sequences)  have been restored
                var minId1 = connector.GenerateUniqueIds("blahblah", 20).Max();
                var minId2 = connector.GenerateUniqueIds("foobar", 20).Max();

                Assert.Greater(minId1, maxId1, "the sequences ware not correctly retored from dump");
                Assert.Greater(minId2, maxId2, "the sequences ware not correctly retored from dump");

                eventAfterDump = dataSource[55555];
                // now it should be null as it was added after dump and we reimported the dump
                Assert.IsNull(eventAfterDump);

                var fixings = dataSource.Count(e => e.EventType == "FIXING");

                Assert.IsTrue(fixings > 50, "fixings > 5000");
            }
        }

#if DEBUG
// this test can work only in debug environment as failure simulations are deactivated in release
        [Test]
        public void In_case_of_failure_during_dump_import_data_is_rollbacked()
        {

        
            var dumpPath = "dump";

            if (Directory.Exists(dumpPath))
                Directory.Delete(dumpPath, true);

            Directory.CreateDirectory(dumpPath);


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

                var admin = connector.AdminInterface();
                admin.Dump(dumpPath);

                dataSource.Put(new FixingEvent(55555, "GLE", 180, "IRD-500"));
            }

            StopServers();
            StartServers();

            using (var connector = new Connector(_clientConfig))
            {
                var dataSource = connector.DataSource<ProductEvent>();

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


                // check that it is still woking fine after rollback
                dataSource.Put(new FixingEvent(66666, "GLE", 180, "IRD-500"));

                var events = new[] {55555, 66666};

                var evts = dataSource.Where(e=> events.Contains(e.EventId)).ToList();

                Assert.AreEqual(2, evts.Count);

            }


            StopServers();
            StartServers();


            // check thatr everything is persisted
            using (var connector = new Connector(_clientConfig))
            {
                var dataSource = connector.DataSource<ProductEvent>();

                var events = new[] { 55555, 66666 };
                var evts = dataSource.Where(e => events.Contains(e.EventId)).ToList();

                Assert.AreEqual(2, evts.Count);

            }
        }
#endif

        [Test]
        public void Check_that_the_order_of_returned_items_is_stable()
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


                var fixings = dataSource.Where(e => e.EventType == "FIXING").ToList();
                for (int i = 0; i < 10; i++)
                {
                    var sameFixings = dataSource.Where(e => e.EventType == "FIXING").ToList();
                    CollectionAssert.AreEqual(fixings, sameFixings);
                }

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
                var dataSource = connector.DataSource<ProductEvent>();

                var events = new List<ProductEvent>();
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

                var admin = connector.AdminInterface();

                string date = DateTime.Today.ToString("yyyy-MM-dd");

                string fullPath = Path.Combine(dumpPath, date);

                admin.InitializeFromDump(fullPath);

                var dataSource = connector.DataSource<ProductEvent>();

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

                Assert.IsTrue(minId1 > maxId1, "the sequences were not resyncronised after reinitializing from dump");
                Assert.IsTrue(minId2 > maxId2, "the sequences were not resyncronised after reinitializing from dump");

            }

            // restart and check that the query gives the same result
            StopServers();

            // add one server to the cluster
            StartServers(ServerCount + 1);

            using (var connector = new Connector(_clientConfig))
            {

                var dataSource = connector.DataSource<ProductEvent>();

                var countAfter = dataSource.Count(e => e.EventType == "INCREASE");

                Assert.AreEqual(count, countAfter);

            }


        }


        [Test]
        public void Generate_unique_ids_with_multiple_threads()
        {
            HashSet<int> all = new HashSet<int>();
            Random rand = new Random(Environment.TickCount);

            using (var connector = new Connector(_clientConfig))
            {

                Parallel.For(0, 1000, i =>
                {
                    var ids = connector.GenerateUniqueIds("test", rand.Next(100));

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
        }

        [Test]
        public void Conditional_put()
        {
            ClientConfig config = new ClientConfig();
            config.LoadFromFile("inprocess_persistent_config.xml");

            using (var connector = new Connector(_clientConfig))
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
            using (var connector = new Connector(_clientConfig))
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
            ClientConfig config = new ClientConfig();
            config.LoadFromFile("inprocess_persistent_config.xml");

            using (var connector = new Connector(_clientConfig))
            {
                var events = connector.DataSource<ProductEvent>();

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
                var events = connector.DataSource<ProductEvent>();

                var reloaded = (FixingEvent)events[1];

                // check that the updated value is persistent
                Assert.AreEqual(160, reloaded.Value);
            }

        }

        [Test]
        public void optimistic_synchronization_with_timestamp()
        {
            ClientConfig config = new ClientConfig();
            config.LoadFromFile("inprocess_persistent_config.xml");

            using (var connector = new Connector(_clientConfig))
            {
                var events = connector.DataSource<ProductEvent>();

                var wasAdded = events.TryAdd(new FixingEvent(1, "AXA", 150, "EQ-256") { Timestamp = DateTime.Now });

                Assert.IsTrue(wasAdded);

                var reloaded = (FixingEvent)events[1];
                var firstVersion = (FixingEvent)events[1];
                


                reloaded.Value = 160;
                
                // first one should work
                events.UpdateWithTimestampSynchronization(reloaded);

                Assert.AreNotEqual(reloaded.Timestamp, firstVersion.Timestamp, "Timestamp should have been updated automatically");
                

                firstVersion.Value = 111;

                // second one should fail as the object has already been modified
                Assert.Throws<CacheException>(() => events.UpdateWithTimestampSynchronization(firstVersion));

            }

            // check also that it has not been saved in the persistence storage
            using (var connector = new Connector(_clientConfig))
            {
                var events = connector.DataSource<ProductEvent>();

                var reloaded = (FixingEvent)events[1];

                // check that the updated value is persistent
                Assert.AreEqual(160, reloaded.Value);
            }

        }
    }
}