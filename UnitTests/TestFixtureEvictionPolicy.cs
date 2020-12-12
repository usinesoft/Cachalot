using System;
using System.Linq;
using System.Threading;
using Client.Core;
using NUnit.Framework;
using Server;
using UnitTests.TestData;
// ReSharper disable ObjectCreationAsStatement

namespace UnitTests
{
    [TestFixture]
    public class TestFixtureEvictionPolicy
    {

        [Test]
        public void LessRecentlyUsed()
        {

            var schema = ClientSideTypeDescription.RegisterType(typeof(CacheableTypeOk)).AsCollectionSchema;

            var policy = new LruEvictionPolicy(10, 2);

            for (var i = 0; i < 100; i++)
            {
                var item = new CacheableTypeOk(i, i + 1000, "aaa", new DateTime(2010, 10, 10), 1500);
                var packed = CachedObject.Pack(item, schema);

                policy.AddItem(packed);
            }

            Assert.IsTrue(policy.IsEvictionRequired);
            var toRemove =  policy.DoEviction();
            Assert.AreEqual(92, toRemove.Count);

            var item93 = new CacheableTypeOk(93, 93 + 1000, "aaa", new DateTime(2010, 10, 10), 1500);
            var packed93 = CachedObject.Pack(item93, schema);

            // check that the 93rd item was not removed
            Assert.IsFalse(toRemove.Any(i=>i == packed93));
            policy.Touch(packed93);

            var item100 = new CacheableTypeOk(100, 100 + 1000, "aaa", new DateTime(2010, 10, 10), 1500);
            var packed100 = CachedObject.Pack(item100, schema);

            var item101 = new CacheableTypeOk(101, 101 + 1000, "aaa", new DateTime(2010, 10, 10), 1500);
            var packed101 = CachedObject.Pack(item101, schema);


            policy.AddItem(packed100);
            policy.AddItem(packed101);

            toRemove = policy.DoEviction();

            Assert.AreEqual(2, toRemove.Count);

            // item 93 was not removed because it was recently used (the call to Touch)
            Assert.IsFalse(toRemove.Any(i=>i == packed93));


        }

        [Test]
        public void LessRecentlyUsedRemoveItem()
        {
            var schema = ClientSideTypeDescription.RegisterType(typeof(CacheableTypeOk)).AsCollectionSchema;

            var policy = new LruEvictionPolicy(10, 2);

            for (var i = 0; i < 9; i++)
            {
                var item = new CacheableTypeOk(i, i + 1000, "aaa", new DateTime(2010, 10, 10), 1500);
                var packed = CachedObject.Pack(item, schema);

                policy.AddItem(packed);
            }

            Assert.IsFalse(policy.IsEvictionRequired);
            var toRemove =  policy.DoEviction();
            Assert.AreEqual(0, toRemove.Count);

            var item1 = new CacheableTypeOk(1, 1 + 1000, "aaa", new DateTime(2010, 10, 10), 1500);
            var packed1 = CachedObject.Pack(item1, schema);

            policy.TryRemove(packed1);

            
            var item10 = new CacheableTypeOk(10, 10 + 1000, "aaa", new DateTime(2010, 10, 10), 1500);
            var packed10 = CachedObject.Pack(item10, schema);

            policy.AddItem(packed10);

            
            // as one item was removed explicitly the eviction should not be triggered yet
            toRemove =  policy.DoEviction();
            Assert.AreEqual(0, toRemove.Count);

            var item11 = new CacheableTypeOk(11, 11 + 1000, "aaa", new DateTime(2010, 10, 10), 1500);
            var packed11 = CachedObject.Pack(item11, schema);

            policy.AddItem(packed11);
            // now the eviction should be triggered
            toRemove =  policy.DoEviction();
            Assert.AreEqual(2, toRemove.Count);

            // the explicitly removed item should not be in the list
            Assert.IsFalse(toRemove.Any(i=>i == packed1));
        }


        [Test]
        public void LessRecentlyUsedLimitCases()
        {
            // the eviction count should be more than 0
            Assert.Throws<ArgumentException>(() => new LruEvictionPolicy(10, 0));

            // the limit should be more than 0
            Assert.Throws<ArgumentException>(() => new LruEvictionPolicy(0, 0));

            // the eviction count should be inferior to the limit
            Assert.Throws<ArgumentException>(() => new LruEvictionPolicy(10, 11));

            // calling DoEviction on an empty policy should do nothing
            var policy = new LruEvictionPolicy(10, 2);
            var toRemove = policy.DoEviction();
            Assert.AreEqual(0, toRemove.Count);

        }


        [Test]
        public void TimeToLive()
        { var schema = ClientSideTypeDescription.RegisterType(typeof(CacheableTypeOk)).AsCollectionSchema;

            var policy = new TtlEvictionPolicy(TimeSpan.FromSeconds(1));
            for (var i = 0; i < 10; i++)
            {
                var item = new CacheableTypeOk(i, i + 1000, "aaa", new DateTime(2010, 10, 10), 1500);
                var packed = CachedObject.Pack(item, schema);

                policy.AddItem(packed);
            }

            Assert.IsFalse(policy.IsEvictionRequired);

            Thread.Sleep(1010);
            
            var toRemove = policy.DoEviction();
            Assert.AreEqual(10, toRemove.Count);

            var item11 = new CacheableTypeOk(11, 11 + 1000, "aaa", new DateTime(2010, 10, 10), 1500);
            var packed11 = CachedObject.Pack(item11, schema);

            policy.AddItem(packed11);
            toRemove = policy.DoEviction();
            Assert.AreEqual(0, toRemove.Count);

            Thread.Sleep(1010);
            
            toRemove = policy.DoEviction();
            Assert.AreEqual(1, toRemove.Count);

        }

        [Test]
        public void TimeToLiveRemoveItem()
        {
            var schema = ClientSideTypeDescription.RegisterType(typeof(CacheableTypeOk)).AsCollectionSchema;

            var policy = new TtlEvictionPolicy(TimeSpan.FromSeconds(1));

            var item11 = new CacheableTypeOk(11, 11 + 1000, "aaa", new DateTime(2010, 10, 10), 1500);
            var packed11 = CachedObject.Pack(item11, schema);

            var item12 = new CacheableTypeOk(12, 12 + 1000, "aaa", new DateTime(2010, 10, 10), 1500);
            var packed12 = CachedObject.Pack(item12, schema);

            policy.AddItem(packed11);
            policy.AddItem(packed12);

            policy.TryRemove(packed11);
            Thread.Sleep(1010);
            
            var toRemove = policy.DoEviction();
            Assert.AreEqual(1, toRemove.Count);
            Assert.AreEqual(packed12, toRemove.Single());


        }

    }
}