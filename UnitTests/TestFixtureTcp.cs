#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Channel;
using Client.Core;
using Client.Queries;
using NUnit.Framework;
using Server;
using UnitTests.TestData;

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

            _server = new Server.Server(new NodeConfig()) {Channel = _serverChannel};
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

            var atomic = builder.MakeAtomicQuery("IndexKeyFolder", "aaa");

            var domainDeclaration = new DomainDescription(new OrQuery(typeof(CacheableTypeOk))
                {Elements = {new AndQuery {Elements = {atomic}}}});

            _client.DeclareDomain(domainDeclaration);

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
                new List<CacheableTypeOk>(_client.GetManyWhere<CacheableTypeOk>("IndexKeyFolder == aaa"));
            Assert.AreEqual(itemsInAaa.Count, 2);

            //change the folder of the first item and put it back into the cache
            item1.IndexKeyFolder = "bbb";
            _client.Put(item1);

            //now it should be only one item left in aaa
            itemsInAaa = new List<CacheableTypeOk>(_client.GetManyWhere<CacheableTypeOk>("IndexKeyFolder == aaa"));
            Assert.AreEqual(itemsInAaa.Count, 1);

            //get both of them again by date
            IList<CacheableTypeOk> allItems =
                new List<CacheableTypeOk>(
                    _client.GetManyWhere<CacheableTypeOk>($"IndexKeyDate ==  {new DateTime(2010, 10, 10).Ticks}"));
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
            for (var i = 0; i < 113; i++)
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
            for (var i = 0; i < 113; i++)
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
            for (var i = 0; i < 1000; i++)
            {
                var item = new CacheableTypeOk(i, 10000 + i, "aaa", new DateTime(2010, 10, 10), 1500 + i);
                _client.Put(item);
            }


            const int clients = 10;

            _requestsFinished = new Semaphore(0, clients);

            //CLIENTS parallel requests: each one uses a channel
            for (var i = 0; i < clients; i++)
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
                            client.GetManyWhere<CacheableTypeOk>("IndexKeyValue < 1700"));
                    Assert.AreEqual(items.Count, 200);

                    _requestsFinished.Release();

                    client.Dispose();
                });


            for (var i = 0; i < clients; i++) _requestsFinished.WaitOne();

            //CLIENTS parallel requests shared channel

            for (var i = 0; i < clients; i++)
                ThreadPool.QueueUserWorkItem(delegate
                {
                    Thread.CurrentThread.Name = "client thread";


                    //reload both items by folder name
                    IList<CacheableTypeOk> items =
                        new List<CacheableTypeOk>(
                            _client.GetManyWhere<CacheableTypeOk>("IndexKeyValue < 1700"));
                    Assert.AreEqual(items.Count, 200);

                    _requestsFinished.Release();
                });


            for (var i = 0; i < clients; i++) _requestsFinished.WaitOne();
        }
    }
}