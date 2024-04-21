#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Channel;
using Client.Core;
using Client.Interface;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Server;
using Tests.TestData;

#endregion

namespace Tests.IntegrationTests
{
    [TestFixture]
    public class TestFixtureEviction
    {
        [SetUp]
        public void Init()
        {
            _client = new DataClient();
            var channel = new InProcessChannel();
            _client.Channel = channel;


            _server = new Server.Server(new NodeConfig()) { Channel = channel };
            _server.Start();


            _schema = _client.DeclareCollection<CacheableTypeOk>();

            CollectionName = _schema.CollectionName;
        }

        [TearDown]
        public void TearDown()
        {
            _server.Stop();
        }

        private string CollectionName { get; set; }

        private DataClient _client;

        private Server.Server _server;

        private CollectionSchema _schema;

        [Test]
        public void EvictionLimitIsReached()
        {
            _client.ConfigEviction(CollectionName, EvictionType.LessRecentlyUsed, 100, 10, 0);

            var desc = _client.GetServerDescription();

            ClassicAssert.IsNotNull(desc);
            ClassicAssert.AreEqual(desc.DataStoreInfoByFullName.Count, 1);

            var info = desc.DataStoreInfoByFullName[CollectionName];

            ClassicAssert.IsNotNull(info);
            ClassicAssert.AreEqual(info.EvictionPolicy, EvictionType.LessRecentlyUsed);

            //add one item
            var item1 = new CacheableTypeOk(1, 1001, "aaa", new DateTime(2010, 10, 10), 1500);
            _client.PutOne(item1);

            //reload the item by primary key
            var item1Reloaded = _client.GetOne<CacheableTypeOk>(i => i.PrimaryKey == 1);
            ClassicAssert.AreEqual(item1, item1Reloaded);

            //add 100 items; eviction should be triggered(10 items should be removed)
            for (var i = 0; i < 100; i++)
            {
                var item = new CacheableTypeOk(i + 2, i + 1002, "aaa", new DateTime(2010, 10, 10), 1500);
                _client.PutOne(item);
            }

            //reload all items
            IList<CacheableTypeOk> itemsInAaa =
                _client.GetMany<CacheableTypeOk>(i => i.IndexKeyFolder == "aaa").ToList();

            ClassicAssert.AreEqual(91, itemsInAaa.Count); //eviction triggered at 100 removed 10 added 1

            var itemsAsList = new List<CacheableTypeOk>(itemsInAaa);
            itemsAsList.Sort((x, y) => x.PrimaryKey.CompareTo(y.PrimaryKey));

            //check that the first 10 items were evicted
            ClassicAssert.IsTrue(itemsAsList[0].PrimaryKey == 11);

            //update the first item. This should prevent it from being evicted
            _client.PutOne(new CacheableTypeOk(11, 1001, "aaa", new DateTime(2010, 10, 10), 1500));

            for (var i = 0; i < 10; i++)
            {
                var item = new CacheableTypeOk(i + 102, i + 1102, "aaa", new DateTime(2010, 10, 10), 1500);
                _client.PutOne(item);
            }

            itemsInAaa = _client.GetMany<CacheableTypeOk>(i => i.IndexKeyFolder == "aaa").ToList();
            ClassicAssert.AreEqual(itemsInAaa.Count, 91); //(100 - 10 +1)


            itemsAsList = new List<CacheableTypeOk>(itemsInAaa);
            itemsAsList.Sort((x, y) => x.PrimaryKey.CompareTo(y.PrimaryKey));

            //check that the item having 11 as primary key was not evicted
            ClassicAssert.AreEqual(11, itemsAsList[0].PrimaryKey);
            ClassicAssert.AreEqual(22, itemsAsList[1].PrimaryKey);
        }

        [Test]
        public void EvictionLimitIsReachedUsingPutMany()
        {
            _client.ConfigEviction(CollectionName, EvictionType.LessRecentlyUsed, 100, 10, 0);

            var desc = _client.GetServerDescription();
            ClassicAssert.IsNotNull(desc);
            ClassicAssert.AreEqual(desc.DataStoreInfoByFullName.Count, 1);

            var info = desc.DataStoreInfoByFullName[CollectionName];
            ClassicAssert.IsNotNull(info);
            ClassicAssert.AreEqual(info.EvictionPolicy, EvictionType.LessRecentlyUsed);

            //add one item
            var item1 = new CacheableTypeOk(1, 1001, "aaa", new DateTime(2010, 10, 10), 1500);
            _client.PutOne(item1);

            //reload the item by primary key
            var item1Reloaded = _client.GetOne<CacheableTypeOk>(i => i.PrimaryKey == 1);
            ClassicAssert.AreEqual(item1, item1Reloaded);

            //add 100 items; eviction should be triggered(10 items should be removed)
            var itemsToPut = new List<CacheableTypeOk>();
            for (var i = 0; i < 100; i++)
            {
                var item = new CacheableTypeOk(i + 2, i + 1002, "aaa", new DateTime(2010, 10, 10), 1500);
                itemsToPut.Add(item);
            }

            _client.PutMany(itemsToPut);

            //reload all items
            IList<CacheableTypeOk> itemsInAaa =
                _client.GetMany<CacheableTypeOk>(i => i.IndexKeyFolder == "aaa").ToList();
            ClassicAssert.AreEqual(itemsInAaa.Count, 90); //(100 - 10)(capacity-evictionCount)
        }

        [Test]
        public void ExcludedItemsAreNotEvicted()
        {
            _client.ConfigEviction(CollectionName, EvictionType.LessRecentlyUsed, 100, 10, 0);

            var itemNotToBeEvicted = new CacheableTypeOk(0, 1000, "aaa", new DateTime(2010, 10, 10), 1500);

            _client.PutOne(itemNotToBeEvicted, true);

            for (var i = 0; i < 101; i++)
            {
                var item = new CacheableTypeOk(i + 1, i + 1001, "aaa", new DateTime(2010, 10, 10), 1500);
                _client.PutOne(item);
            }

            IList<CacheableTypeOk> itemsInAaa =
                _client.GetMany<CacheableTypeOk>(i => i.IndexKeyFolder == "aaa").ToList();
            var itemsAsList = new List<CacheableTypeOk>(itemsInAaa);
            itemsAsList.Sort((x, y) => x.PrimaryKey.CompareTo(y.PrimaryKey));

            //check that the first item was not evicted
            ClassicAssert.AreEqual(92, itemsAsList.Count);
            ClassicAssert.IsTrue(itemsAsList[0].PrimaryKey == 0);
        }


        [Test]
        public void TtlEviction()
        {
            _client.ConfigEviction(nameof(CacheableTypeOk), EvictionType.TimeToLive, 0, 0, 1000);


            // this one should be evicted because it will expire
            var itemToBeEvicted = new CacheableTypeOk(1, 1001, "aaa", new DateTime(2010, 10, 10), 1500);
            _client.PutOne(itemToBeEvicted);

            // this one should not be evicted because even if it will expire it is marked as "exclude from eviction"
            var itemToKeepForever = new CacheableTypeOk(15, 1002, "aaa", new DateTime(2010, 10, 10), 1500);
            _client.PutOne(itemToKeepForever, true);

            Thread.Sleep(1500);

            // this item should not be evicted because it has not expired
            var itemNotToBeEvicted = new CacheableTypeOk(0, 1000, "aaa", new DateTime(2010, 10, 10), 1500);
            _client.PutOne(itemNotToBeEvicted);

            IList<CacheableTypeOk> itemsInAaa =
                _client.GetMany<CacheableTypeOk>(i => i.IndexKeyFolder == "aaa").ToList();

            ClassicAssert.AreEqual(2, itemsInAaa.Count);
            ClassicAssert.IsFalse(itemsInAaa.Any(i => i.PrimaryKey == 1), "only item 1 should have been evicted");
        }
    }
}