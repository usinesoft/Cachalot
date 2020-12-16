#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Channel;
using Client.Core;
using Client.Interface;
using Client.Queries;
using NUnit.Framework;
using Server;
using Tests.TestData;

#endregion

namespace Tests.IntegrationTests
{
    [TestFixture]
    public class TestFixtureClientServer
    {
        [SetUp]
        public void Init()
        {
            _client = new DataClient();
            var channel = new InProcessChannel();
            _client.Channel = channel;


            _server = new Server.Server(new NodeConfig()) {Channel = channel};
            _server.Start();

            _client.DeclareCollection<CacheableTypeOk>();
            _client.DeclareCollection<Trade>();
        }

        [TearDown]
        public void Exit()
        {
            _client.Dispose();
            _server.Stop();
        }

        private DataClient _client;

        private Server.Server _server;


        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            Environment.CurrentDirectory = TestContext.CurrentContext.TestDirectory;
            Directory.SetCurrentDirectory(TestContext.CurrentContext.TestDirectory);
        }


        [Test]
        public void DataAccess()
        {
            //add two new items
            var item1 = new CacheableTypeOk(1, 1001, "aaa", new DateTime(2010, 10, 10), 1500);
            _client.PutOne(item1);

            var item2 = new CacheableTypeOk(2, 1002, "aaa", new DateTime(2010, 10, 10), 1600);
            _client.PutOne(item2);

            //reload the first one by primary key
            var item1Reloaded = _client.GetOne<CacheableTypeOk>(i => i.PrimaryKey == 1);
            Assert.AreEqual(item1, item1Reloaded);

            //try to load nonexistent object by primary key
            var itemNull = _client.GetOne<CacheableTypeOk>(i => i.PrimaryKey == 2055);
            Assert.IsNull(itemNull);

            //try to load nonexistent object by unique key
            itemNull = _client.GetOne<CacheableTypeOk>(i => i.UniqueKey == 55);
            Assert.IsNull(itemNull);

            //reload both items by folder name
            IList<CacheableTypeOk> itemsInAaa =
                _client.GetMany<CacheableTypeOk>(i => i.IndexKeyFolder == "aaa").ToList();
            Assert.AreEqual(itemsInAaa.Count, 2);

            //change the folder of the first item and put it back into the cache
            item1.IndexKeyFolder = "bbb";
            _client.PutOne(item1);

            //now it should be only one item left in aaa
            itemsInAaa = _client.GetMany<CacheableTypeOk>(i => i.IndexKeyFolder == "aaa").ToList();
            Assert.AreEqual(itemsInAaa.Count, 1);

            //get both of them again by date
            IList<CacheableTypeOk> allItems = _client
                .GetMany<CacheableTypeOk>(i => i.IndexKeyDate == new DateTime(2010, 10, 10)).ToList();

            Assert.AreEqual(allItems.Count, 2);


            //get both of them using an In query

            var folders = new[] {"aaa", "bbb"};
            allItems = _client.GetMany<CacheableTypeOk>(i => folders.Contains(i.IndexKeyFolder)).ToList();
            Assert.AreEqual(allItems.Count, 2);

            //remove the first one
            _client.RemoveMany<CacheableTypeOk>(i => i.PrimaryKey == 1);

            //removing non existent item should not throw exception
            var removed = _client.RemoveMany<CacheableTypeOk>(i => i.PrimaryKey == 46546);
            Assert.AreEqual(0, removed);

            //COUNT should also return 1
            var count = _client.Count<CacheableTypeOk>(i => folders.Contains(i.IndexKeyFolder));
            Assert.AreEqual(count, 1);
        }

        [Test]
        public void DomainDescription()
        {
            var evalResult = _client.IsComplete<CacheableTypeOk>(i => i.IndexKeyFolder == "aaa");
            Assert.IsFalse(evalResult);


            var item1 = new CacheableTypeOk(1, 1001, "aaa", new DateTime(2010, 10, 10), 1500);
            _client.PutOne(item1);

            var item2 = new CacheableTypeOk(2, 1002, "aaa", new DateTime(2010, 10, 10), 1600);
            _client.PutOne(item2);

            evalResult = _client.IsComplete<CacheableTypeOk>(i => i.IndexKeyFolder == "aaa");
            Assert.IsFalse(evalResult);

            var description = new DomainDescription(OrQuery.Empty<CacheableTypeOk>());

            //describe the domain as complete
            _client.DeclareDomain(description);

            evalResult = _client.IsComplete<CacheableTypeOk>(i => i.IndexKeyFolder == "aaa");
            Assert.IsTrue(evalResult);
        }

        [Test]
        public void GetServerInfo()
        {
            var desc = _client.GetServerDescription();
            Assert.AreEqual(2, desc.KnownTypesByFullName.Count);

            Assert.AreEqual(2, desc.DataStoreInfoByFullName.Count);
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


            // 100 parallel requests
            Parallel.For(0, 10, i =>
            {
                IList<CacheableTypeOk> items =
                    _client.GetMany<CacheableTypeOk>(i => i.IndexKeyValue < 1700)
                        .ToList();
                Assert.AreEqual(items.Count, 200);
            });
        }


        [Test]
        public void SearchByIndexedList()
        {
            //add two new items
            var trade1 = new Trade(1, 1001, "aaa", new DateTime(2010, 10, 10), 1500) {Accounts = {1, 101, 10001, 7}};
            _client.PutOne(trade1);

            var trade2 = new Trade(2, 1002, "bbbb", new DateTime(2010, 10, 10), 1600) {Accounts = {2, 102, 10002, 7}};
            _client.PutOne(trade2);


            var all = _client.GetMany<Trade>(t => t.Accounts.Contains(101)).ToList();

            Assert.AreEqual(all.Count, 1);

            all = _client.GetMany<Trade>(t => t.Accounts.Contains(7)).ToList();

            Assert.AreEqual(all.Count, 2);
        }

        [Test]
        public void Truncate()
        {
            //add two new items
            var item1 = new CacheableTypeOk(1, 1001, "aaa", new DateTime(2010, 10, 10), 1500);
            _client.PutOne(item1);

            var item2 = new CacheableTypeOk(2, 1002, "aaa", new DateTime(2010, 10, 10), 1600);
            _client.PutOne(item2);

            IList<CacheableTypeOk> itemsInAaa =
                _client.GetMany<CacheableTypeOk>(i => i.IndexKeyFolder == "aaa").ToList();
            Assert.AreEqual(itemsInAaa.Count, 2);

            var collectionName = typeof(CacheableTypeOk).FullName ?? throw new InvalidOperationException();

            var desc = _client.GetServerDescription();
            var hits = desc.DataStoreInfoByFullName[collectionName].HitCount;

            Assert.AreEqual(hits, 1);

            _client.Truncate(collectionName);

            desc = _client.GetServerDescription();
            hits = desc.DataStoreInfoByFullName[collectionName].HitCount;
            var count = desc.DataStoreInfoByFullName[collectionName].Count;

            Assert.AreEqual(hits, 0);
            Assert.AreEqual(count, 0);
        }
    }
}