#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Channel;
using Client.Core;
using Client.Interface;
using NUnit.Framework;
using Server;
using UnitTests.TestData;

#endregion

namespace UnitTests
{
    [TestFixture]
    public class TestFixtureEviction
    {
        [SetUp]
        public void Init()
        {
            _client = new CacheClient();
            var channel = new InProcessChannel();
            _client.Channel = channel;


            _server = new Server.Server(new NodeConfig()) {Channel = channel};
            _server.Start();

            _client.RegisterTypeIfNeeded(typeof(CacheableTypeOk));
        }

        [TearDown]
        public void TearDown()
        {
            _server.Stop();
        }

        private CacheClient _client;

        private Server.Server _server;

        [Test]
        public void EvictionLimitIsReached()
        {
            _client.ConfigEviction(typeof(CacheableTypeOk).FullName, EvictionType.LessRecentlyUsed, 100, 10);
            var desc = _client.GetServerDescription();
            Assert.IsNotNull(desc);
            Assert.AreEqual(desc.DataStoreInfoByFullName.Count, 1);
            var info = desc.DataStoreInfoByFullName[typeof(CacheableTypeOk).FullName];
            Assert.IsNotNull(info);
            Assert.AreEqual(info.EvictionPolicy, EvictionType.LessRecentlyUsed);

            //add one item
            var item1 = new CacheableTypeOk(1, 1001, "aaa", new DateTime(2010, 10, 10), 1500);
            _client.Put(item1);

            //reload the item by primary key
            var item1Reloaded = _client.GetOne<CacheableTypeOk>(1);
            Assert.AreEqual(item1, item1Reloaded);

            //add 100 items; eviction should be triggered(10 items should be removed)
            for (var i = 0; i < 100; i++)
            {
                var item = new CacheableTypeOk(i + 2, i + 1002, "aaa", new DateTime(2010, 10, 10), 1500);
                _client.Put(item);
            }

            //reload all items

            IList<CacheableTypeOk> itemsInAaa = _client.GetManyWhere<CacheableTypeOk>("IndexKeyFolder == aaa").ToList();
            Assert.AreEqual(91, itemsInAaa.Count); //eviction triggered at 100 removed 10 added 1

            var itemsAsList = new List<CacheableTypeOk>(itemsInAaa);
            itemsAsList.Sort((x, y) => x.PrimaryKey.CompareTo(y.PrimaryKey));

            //check that the first 10 items were evicted
            Assert.IsTrue(itemsAsList[0].PrimaryKey == 11);

            //update the first item. This should prevent it from being evicted
            _client.Put(new CacheableTypeOk(11, 1001, "aaa", new DateTime(2010, 10, 10), 1500));

            for (var i = 0; i < 10; i++)
            {
                var item = new CacheableTypeOk(i + 102, i + 1102, "aaa", new DateTime(2010, 10, 10), 1500);
                _client.Put(item);
            }

            itemsInAaa = _client.GetManyWhere<CacheableTypeOk>("IndexKeyFolder == aaa").ToList();
            Assert.AreEqual(itemsInAaa.Count, 91); //(100 - 10 +1)


            itemsAsList = new List<CacheableTypeOk>(itemsInAaa);
            itemsAsList.Sort((x, y) => x.PrimaryKey.CompareTo(y.PrimaryKey));

            //check that the item having 11 as primary key was not evicted
            Assert.AreEqual(11, itemsAsList[0].PrimaryKey);
            Assert.AreEqual(22, itemsAsList[1].PrimaryKey);
        }

        [Test]
        public void EvictionLimitIsReachedUsingPutMany()
        {
            _client.ConfigEviction(typeof(CacheableTypeOk).FullName, EvictionType.LessRecentlyUsed, 100, 10);
            var desc = _client.GetServerDescription();
            Assert.IsNotNull(desc);
            Assert.AreEqual(desc.DataStoreInfoByFullName.Count, 1);
            var info = desc.DataStoreInfoByFullName[typeof(CacheableTypeOk).FullName];
            Assert.IsNotNull(info);
            Assert.AreEqual(info.EvictionPolicy, EvictionType.LessRecentlyUsed);

            //add one item
            var item1 = new CacheableTypeOk(1, 1001, "aaa", new DateTime(2010, 10, 10), 1500);
            _client.Put(item1);

            //reload the item by primary key
            var item1Reloaded = _client.GetOne<CacheableTypeOk>(1);
            Assert.AreEqual(item1, item1Reloaded);

            //add 100 items; eviction should be triggered(10 items should be removed)
            var itemsToPut = new List<CacheableTypeOk>();
            for (var i = 0; i < 100; i++)
            {
                var item = new CacheableTypeOk(i + 2, i + 1002, "aaa", new DateTime(2010, 10, 10), 1500);
                itemsToPut.Add(item);
            }

            _client.PutMany(itemsToPut, false);

            //reload all items
            IList<CacheableTypeOk> itemsInAaa = _client.GetManyWhere<CacheableTypeOk>("IndexKeyFolder == aaa").ToList();
            Assert.AreEqual(itemsInAaa.Count, 90); //(100 - 10)(capacity-evictionCount)
        }

        [Test]
        public void ExcludedItemsAreNotEvicted()
        {
            _client.ConfigEviction(typeof(CacheableTypeOk).FullName, EvictionType.LessRecentlyUsed, 100, 10);

            var itemNotToBeEvicted = new CacheableTypeOk(0, 1000, "aaa", new DateTime(2010, 10, 10), 1500);
            _client.Put(itemNotToBeEvicted, true);

            for (var i = 0; i < 101; i++)
            {
                var item = new CacheableTypeOk(i + 1, i + 1001, "aaa", new DateTime(2010, 10, 10), 1500);
                _client.Put(item);
            }

            IList<CacheableTypeOk> itemsInAaa = _client.GetManyWhere<CacheableTypeOk>("IndexKeyFolder == aaa").ToList();
            var itemsAsList = new List<CacheableTypeOk>(itemsInAaa);
            itemsAsList.Sort((x, y) => x.PrimaryKey.CompareTo(y.PrimaryKey));

            //check that the first item was not evicted
            Assert.AreEqual(92, itemsAsList.Count);
            Assert.IsTrue(itemsAsList[0].PrimaryKey == 0);
        }


        [Test]
        public void TtlEviction()
        {
            _client.ConfigEviction(typeof(CacheableTypeOk).FullName, EvictionType.TimeToLive, 0, 0, 1000);

            // this item should not be evicted
            var itemNotToBeEvicted = new CacheableTypeOk(0, 1000, "aaa", new DateTime(2010, 10, 10), 1500);
            _client.Put(itemNotToBeEvicted, true);

            var itemToBeEvicted = new CacheableTypeOk(1, 1001, "aaa", new DateTime(2010, 10, 10), 1500);
            _client.Put(itemNotToBeEvicted, true);

            Thread.Sleep(1500);

            IList<CacheableTypeOk> itemsInAaa = _client.GetManyWhere<CacheableTypeOk>("IndexKeyFolder == aaa").ToList();

            Assert.AreEqual(1, itemsInAaa.Count);
            Assert.AreEqual(0, itemsInAaa.Single().PrimaryKey);
        }

    }
}