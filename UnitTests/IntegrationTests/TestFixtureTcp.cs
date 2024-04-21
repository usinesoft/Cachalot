#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Channel;
using Client.Core;
using Client.Interface;
using Client.Messages;
using Client.Queries;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Server;
using Tests.TestData;

#endregion

namespace Tests.IntegrationTests
{
    [TestFixture]
    public class TestFixtureTcp
    {
        [SetUp]
        public void Init()
        {
            _serverChannel = new TcpServerChannel();

            _server = new Server.Server(new NodeConfig()) { Channel = _serverChannel };
            _serverPort = _serverChannel.Init();
            _serverChannel.Start();
            _server.Start();

            _client = NewClient();


            _client.DeclareCollection<CacheableTypeOk>();
        }

        [TearDown]
        public void Exit()
        {
            _serverChannel.Stop();
            _server.Stop();
        }

        private TcpServerChannel _serverChannel;
        private Server.Server _server;
        private DataClient _client;
        private int _serverPort;

        private DataClient NewClient()
        {
            var client = new DataClient
            {
                Channel =
                    new TcpClientChannel(new TcpClientPool(4, 1,
                        "localhost",
                        _serverPort))
            };

            return client;
        }


        [Test]
        public void DataAccess()
        {
            //add two new items
            var item1 = new CacheableTypeOk(1, 1001, "aaa", new DateTime(2010, 10, 10), 1500);
            _client.PutOne(item1);

            var item2 = new CacheableTypeOk(2, 1002, "aaa", new DateTime(2010, 10, 10), 1600);
            _client.PutOne(item2);

            _client.DeclareDomain<CacheableTypeOk>(x => x.IndexKeyFolder == "aaa");

            var eval = _client.IsComplete<CacheableTypeOk>(x => x.IndexKeyFolder == "aaa");
            ClassicAssert.IsTrue(eval); //domain is complete


            eval = _client.IsComplete<CacheableTypeOk>(x => x.IndexKeyFolder == "bbb");
            ClassicAssert.IsFalse(eval); //domain is incomplete


            //reload the first one by primary key
            var item1Reloaded = _client.GetOne<CacheableTypeOk>(i => i.PrimaryKey == 1);
            ClassicAssert.AreEqual(item1, item1Reloaded);

            //reload both items by folder name
            var itemsInAaa = _client.GetMany<CacheableTypeOk>(x => x.IndexKeyFolder == "aaa").ToList();

            ClassicAssert.AreEqual(2, itemsInAaa.Count);

            //change the folder of the first item and put it back into the cache
            item1.IndexKeyFolder = "bbb";
            _client.PutOne(item1);

            //now it should be only one item left in aaa
            itemsInAaa = _client.GetMany<CacheableTypeOk>(x => x.IndexKeyFolder == "aaa").ToList();
            ClassicAssert.AreEqual(1, itemsInAaa.Count);

            //get both of them again by date
            var allItems = _client.GetMany<CacheableTypeOk>(x => x.IndexKeyDate == new DateTime(2010, 10, 10)).ToList();
            ClassicAssert.AreEqual(allItems.Count, 2);

            //get both of them using an In query

            var folders = new[] { "aaa", "bbb" };

            allItems = _client.GetMany<CacheableTypeOk>(x => folders.Contains(x.IndexKeyFolder)).ToList();
            ClassicAssert.AreEqual(allItems.Count, 2);

            //remove the first one
            _client.RemoveMany<CacheableTypeOk>(x => x.PrimaryKey == 1);

            //the previous query should now return only one item
            allItems = _client.GetMany<CacheableTypeOk>(x => folders.Contains(x.IndexKeyFolder)).ToList();
            ClassicAssert.AreEqual(allItems.Count, 1);
        }

        [Test]
        public void FeedDataToCache()
        {
            var items = new List<CacheableTypeOk>();
            for (var i = 0; i < 113; i++)
            {
                var item1 = new CacheableTypeOk(i, 1000 + i, "aaa", new DateTime(2010, 10, 10), 1500);
                items.Add(item1);
            }


            _client.PutMany(items, true);

            IList<CacheableTypeOk> itemsReloaded =
                _client.GetMany<CacheableTypeOk>(x => x.IndexKeyFolder == "aaa").ToList();

            ClassicAssert.AreEqual(itemsReloaded.Count, 113);
        }

        [Test]
        public void SpeedTestReadOne()
        {
            var items = new List<CacheableTypeOk>();
            for (var i = 0; i < 50000; i++)
            {
                var item1 = new CacheableTypeOk(i, 1000 + i, "aaa", new DateTime(2010, 10, 10), 1500);
                items.Add(item1);
            }


            _client.PutMany(items, true);


            Console.WriteLine("With Newtonsoft Json");
            var watch = new Stopwatch();
            watch.Start();

            const int count = 10_000;
            for (var i = 0; i < count; i++)
            {
                var query = new OrQuery(nameof(CacheableTypeOk))
                {
                    Elements =
                    {
                        new AndQuery
                        {
                            Elements =
                            {
                                new AtomicQuery(new KeyInfo("PrimaryKey", 0, IndexType.Primary), new KeyValue(i),
                                    QueryOperator.Eq)
                            }
                        }
                    }
                };
                var _ = _client.GetMany(query).FirstOrDefault();

                
            }
            
            watch.Stop();

            Console.WriteLine($"Took {watch.ElapsedMilliseconds} ms for {count} items avg={watch.ElapsedMilliseconds / (float)count}");

            Console.WriteLine("With System.Text.Json");
            watch.Restart();

            
            for (var i = 0; i < count; i++)
            {
                var query = new OrQuery(nameof(CacheableTypeOk))
                {
                    Elements =
                    {
                        new AndQuery
                        {
                            Elements =
                            {
                                new AtomicQuery(new KeyInfo("PrimaryKey", 0, IndexType.Primary), new KeyValue(i),
                                    QueryOperator.Eq)
                            }
                        }
                    }
                };
                var _ = _client.GetMany(query).FirstOrDefault();

                
            }
            
            watch.Stop();

            Console.WriteLine($"Took {watch.ElapsedMilliseconds} ms for {count} items avg={watch.ElapsedMilliseconds / (float)count}");
        }


        [Test]
        public void MultiThreadedDataAccess()
        {
            //first load data 
            for (var i = 0; i < 1000; i++)
            {
                var item = new CacheableTypeOk(i, 10000 + i, "aaa", new DateTime(2010, 10, 10), 1500 + i);
                _client.PutOne(item);
            }

            const int clients = 10;

            // parallel requests, each client has its own channel 
            Parallel.For(0, clients, i =>
            {
                using var client = new DataClient();
                var clientChannel =
                    new TcpClientChannel(new TcpClientPool(1, 1, "localhost",
                        _serverPort));

                client.Channel = clientChannel;

                IList<CacheableTypeOk> items = client.GetMany<CacheableTypeOk>(x => x.IndexKeyValue < 1700).ToList();
                ClassicAssert.AreEqual(items.Count, 200);
            });

            var sharedChannel = new TcpClientChannel(new TcpClientPool(1, 1, "localhost", _serverPort));
            using var client = new DataClient { Channel = sharedChannel };
            
            // parallel requests, shared channel (a if it is used in a server back-end)
            Parallel.For(0, clients, i =>
            {
                
                IList<CacheableTypeOk> items = client.GetMany<CacheableTypeOk>(x => x.IndexKeyValue < 1700).ToList();
                ClassicAssert.AreEqual(items.Count, 200);
            });
        }
    }
}