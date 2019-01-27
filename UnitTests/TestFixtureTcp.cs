#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Channel;
using Client.Core;
using Client.Interface;
using Client.Messages;
using Client.Queries;
using NUnit.Framework;
using UnitTests.TestData;
using ServerConfig = Server.ServerConfig;


#endregion

namespace UnitTests
{
    [TestFixture]
    public class TestFixtureTcp
    {
        [SetUp]
        public void Init()
        {
            _serverChannel = new TcpServerChannel();

            _server = new Server.Server(new ServerConfig()) {Channel = _serverChannel};
            _serverPort = _serverChannel.Init();
            _serverChannel.Start();
            _server.Start();

            Thread.Sleep(500); //be sure the server is started

            _client = NewClient();

            
            _client.RegisterTypeIfNeeded(typeof(CacheableTypeOk));
        }

        [TearDown]
        public void Exit()
        {
            _serverChannel.Stop();
            _server.Stop();
        }

        private TcpServerChannel _serverChannel;
        private Server.Server _server;
        private CacheClient _client;
        private int _serverPort;

        private CacheClient NewClient()
        {
            var client = new CacheClient
            {
                Channel =
                    new TcpClientChannel(new TcpClientPool(4, 1,
                        "localhost",
                        _serverPort))
            };

            return client;
        }


        private Semaphore _requestsFinished;

        [Test]
        public void DataAccess()
        {
            var builder = new QueryBuilder(typeof(CacheableTypeOk));

            //add two new items
            var item1 = new CacheableTypeOk(1, 1001, "aaa", new DateTime(2010, 10, 10), 1500);
            _client.Put(item1);

            var item2 = new CacheableTypeOk(2, 1002, "aaa", new DateTime(2010, 10, 10), 1600);
            _client.Put(item2);

            var domainDeclaration = new DomainDescription(typeof(CacheableTypeOk));
            domainDeclaration.AddOrReplace(builder.MakeAtomicQuery("IndexKeyFolder", "aaa"));
            _client.DeclareDomain(domainDeclaration, DomainDeclarationAction.Set);

            var eval = _client.EvalQuery(builder.GetManyWhere("IndexKeyFolder == aaa"));
            Assert.IsTrue(eval.Key); //domain is complete
            Assert.AreEqual(eval.Value, 2);

            eval = _client.EvalQuery(builder.GetManyWhere("IndexKeyFolder == bbb"));
            Assert.IsFalse(eval.Key); //domain is incomplete
            Assert.AreEqual(eval.Value, 0);

            //reload the first one by primary key
            var item1Reloaded = _client.GetOne<CacheableTypeOk>(1);
            Assert.AreEqual(item1, item1Reloaded);

            //reload both items by folder name
            IList<CacheableTypeOk> itemsInAaa =
                new List<CacheableTypeOk>(_client.GetMany<CacheableTypeOk>("IndexKeyFolder == aaa"));
            Assert.AreEqual(itemsInAaa.Count, 2);

            //change the folder of the first item and put it back into the cache
            item1.IndexKeyFolder = "bbb";
            _client.Put(item1);

            //now it should be only one item left in aaa
            itemsInAaa = new List<CacheableTypeOk>(_client.GetMany<CacheableTypeOk>("IndexKeyFolder == aaa"));
            Assert.AreEqual(itemsInAaa.Count, 1);

            //get both of them again by date
            IList<CacheableTypeOk> allItems =
                new List<CacheableTypeOk>(_client.GetMany<CacheableTypeOk>($"IndexKeyDate ==  {new DateTime(2010, 10, 10).Ticks}"));
            Assert.AreEqual(allItems.Count, 2);

            //get both of them using an In query

            var q = builder.In("indexkeyfolder", "aaa", "bbb");
            allItems = _client.GetMany<CacheableTypeOk>(q).ToList();
            Assert.AreEqual(allItems.Count, 2);

            //remove the first one
            _client.Remove<CacheableTypeOk>(1);

            //the previous query should now return only one item
            allItems = _client.GetMany<CacheableTypeOk>(q).ToList();
            Assert.AreEqual(allItems.Count, 1);
        }

        [Test]
        public void FeedDataToCache()
        {
            var items = new List<CacheableTypeOk>();
            for (int i = 0; i < 113; i++)
            {
                var item1 = new CacheableTypeOk(i, 1000 + i, "aaa", new DateTime(2010, 10, 10), 1500);
                items.Add(item1);
            }


            _client.FeedMany(items, true);

            IList<CacheableTypeOk> itemsReloaded =
                _client.GetManyWhere<CacheableTypeOk>("IndexKeyFolder == aaa").ToList();
            Assert.AreEqual(itemsReloaded.Count, 113);

        }

        [Test]
        public void FeedDataToCacheUsingSessions()
        {
            var session = _client.BeginFeed<CacheableTypeOk>(10, false);
            for (int i = 0; i < 113; i++)
            {
                var item1 = new CacheableTypeOk(i, 1000 + i, "aaa", new DateTime(2010, 10, 10), 1500);
                _client.Add(session, item1);
            }


            _client.EndFeed<CacheableTypeOk>(session);

            Thread.Sleep(300);

            IList<CacheableTypeOk> itemsReloaded =
                _client.GetManyWhere<CacheableTypeOk>("IndexKeyFolder == aaa").ToList();
            Assert.AreEqual(itemsReloaded.Count, 113);
        }

        [Test]
        public void MultiThreadedDataAccess()
        {
            //first load data 
            for (int i = 0; i < 1000; i++)
            {
                var item = new CacheableTypeOk(i, 10000 + i, "aaa", new DateTime(2010, 10, 10), 1500 + i);
                _client.Put(item);
            }


            const int clients = 10;

            _requestsFinished = new Semaphore(0, clients);

            //CLIENTS parallel requests: each one uses a channel
            for (int i = 0; i < clients; i++)
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    Thread.CurrentThread.Name = "client thread";
                    var client = new CacheClient();
                    var clientChannel =
                        new TcpClientChannel(new TcpClientPool(1, 1, "localhost",
                            _serverPort));

                    client.Channel = clientChannel;


                    //reload both items by folder name
                    IList<CacheableTypeOk> items =
                        new List<CacheableTypeOk>(
                            client.GetMany<CacheableTypeOk>("IndexKeyValue < 1700"));
                    Assert.AreEqual(items.Count, 200);

                    _requestsFinished.Release();

                    client.Dispose();
                });
            }


            for (int i = 0; i < clients; i++)
            {
                _requestsFinished.WaitOne();
            }

            //CLIENTS parallel requests shared channel

            for (int i = 0; i < clients; i++)
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    Thread.CurrentThread.Name = "client thread";


                    //reload both items by folder name
                    IList<CacheableTypeOk> items =
                        new List<CacheableTypeOk>(
                            _client.GetMany<CacheableTypeOk>("IndexKeyValue < 1700"));
                    Assert.AreEqual(items.Count, 200);

                    _requestsFinished.Release();
                });
            }


            for (int i = 0; i < clients; i++)
            {
                _requestsFinished.WaitOne();
            }
        }


        [Test]
        public void StreamAvailableObjects()
        {
            //add two new items
            var item1 = new CacheableTypeOk(1, 1001, "aaa", new DateTime(2010, 10, 10), 1500);
            _client.Put(item1);

            var item2 = new CacheableTypeOk(2, 1002, "aaa", new DateTime(2010, 10, 10), 1600);
            _client.Put(item2);

            //ask for items 1 2 3 4 (1 2 should be returned and 3 4 not found)
            var items = new List<KeyValue>
            {
                new KeyValue(1,
                    new KeyInfo(KeyDataType.IntKey, KeyType.Primary,
                        "PrimaryKey")),
                new KeyValue(2,
                    new KeyInfo(KeyDataType.IntKey, KeyType.Primary,
                        "PrimaryKey")),
                new KeyValue(3,
                    new KeyInfo(KeyDataType.IntKey, KeyType.Primary,
                        "PrimaryKey")),
                new KeyValue(4,
                    new KeyInfo(KeyDataType.IntKey, KeyType.Primary,
                        "PrimaryKey"))
            };

            var wait = new ManualResetEvent(false);
            var found = new List<CacheableTypeOk>();
            var notFound = _client.GetAvailableItems(items,
                delegate(CacheableTypeOk item, int currentItem, int totalItems)
                {
                    found.Add(item);
                    if (currentItem == totalItems)
                        wait.Set();
                }, delegate { });

            wait.WaitOne();
            Assert.AreEqual(notFound.Count, 2);
            Assert.AreEqual(notFound[0], 3);
            Assert.AreEqual(notFound[1], 4);
            Assert.AreEqual(found.Count, 2);
            Assert.AreEqual(found[0].PrimaryKey, 1);
            Assert.AreEqual(found[1].PrimaryKey, 2);
        }

        

        [Test]
        public void TestWithDataCompression()
        {
            //test with a type using protocol buffers
            var persons = new List<FatPerson>(113);
            for (int i = 0; i < 113; i++)
            {
                var person = new FatPerson(i, "dan", "ionescu");
                persons.Add(person);
            }

            _client.FeedMany(persons, true);
            //var personsReloaded = _client.GetMany<FatPerson>(p => p.First == "dan").ToList();
            //Assert.AreEqual(personsReloaded.Count, 113);
            //Assert.AreEqual(personsReloaded[0].Id, 0);
            //Assert.AreEqual(personsReloaded[0].First, "dan");
            //Assert.AreEqual(personsReloaded[0].Last, "ionescu");

            var p3 = _client.GetOne<FatPerson>(3);
            Assert.IsNotNull(p3);
            Assert.AreEqual(p3.Id, 3);

            _client.Remove<FatPerson>(3);
            //personsReloaded = _client.GetManyWhere<FatPerson>("first==dan").ToList();
            //Assert.AreEqual(personsReloaded.Count, 112);


            var querySet = new List<KeyValue>
            {
                new KeyValue(0, new KeyInfo(KeyDataType.IntKey, KeyType.Primary, "Id")),
                new KeyValue(1, new KeyInfo(KeyDataType.IntKey, KeyType.Primary, "Id")),
                new KeyValue(2, new KeyInfo(KeyDataType.IntKey, KeyType.Primary, "Id")),
                new KeyValue(3, new KeyInfo(KeyDataType.IntKey, KeyType.Primary, "Id")),
                new KeyValue(4, new KeyInfo(KeyDataType.IntKey, KeyType.Primary, "Id"))
            };

            long found = 0;

            var missing = _client.GetAvailableItems<FatPerson>(querySet, delegate { Interlocked.Increment(ref found); },
                delegate { Assert.Fail("exception response from server"); });

            while (Interlocked.Read(ref found) < 4)
            {
                Thread.Sleep(10);
            }

            Assert.AreEqual(missing.Count, 1);
            Assert.AreEqual(missing[0].ToString(), "#3");
        }
    }
}