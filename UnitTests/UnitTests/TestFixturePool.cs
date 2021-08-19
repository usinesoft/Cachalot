using System.Threading;
using NUnit.Framework;

namespace Tests.UnitTests
{
    [TestFixture]
    public class TestFixturePool
    {
        [Test]
        public void DoNotClaimExternalResourcesIfThePoolIsNotEmpty()
        {
            var pool = new SlowProviderPool(4);

            //this call will claim an external resource
            pool.Get();
            Assert.AreEqual(pool.NewResourceClaims, 1);

            //this one too
            var res2 = pool.Get();
            Assert.AreEqual(pool.NewResourceClaims, 2);

            //now we put a resource back to the pool
            pool.Put(res2);

            //this call should be served with the pooled resource
            var res3 = pool.Get();
            Assert.AreEqual(pool.NewResourceClaims, 2);

            //invalidate a resource and put it back to the pool
            res3.IsValid = false;
            pool.Put(res3);

            //this call will claim an external resource as the one available in the pool is
            //not valid any more
            pool.Get();
            Assert.AreEqual(4, pool.NewResourceClaims);

        }

        [Test]
        public void DoNotClaimUnlimitedNewResources()
        {
            var pool = new SlowProviderPool(4);

            long count = 0;
            for (var i = 0; i < 10; i++)
                ThreadPool.QueueUserWorkItem(delegate
                {
                    var res = pool.Get();
                    Thread.Sleep(50);
                    pool.Put(res);
                    // ReSharper disable once AccessToModifiedClosure
                    Interlocked.Increment(ref count);
                });

            while (Interlocked.Read(ref count) < 10) Thread.Sleep(10);

            var claimCount = pool.NewResourceClaims;

            //as consumer threads are much faster than the resource provider, most of the connections
            //are recycled ones not new ones
            Assert.IsTrue(claimCount < 100);
        }

        //[Test]
        //public void PreloadedResourcePool()
        //{
        //    var pool = new SlowProviderPool(4, 3);

        //    Assert.AreEqual(pool.NewResourceClaims, 3);

        //    Assert.AreEqual(pool.ResourcesInPool, 3);

        //    //three requests should be served by the pool without claiming external resource
        //    var res1 = pool.Get();
        //    var res2 = pool.Get();
        //    var res3 = pool.Get();

        //    Assert.AreEqual(3, pool.NewResourceClaims, 3);

        //    //this one is claimed from outside
        //    var res4 = pool.Get();
        //    Assert.AreEqual(4,pool.NewResourceClaims);

        //    //this one too
        //    var res5 = pool.Get();
        //    Assert.AreEqual(5, pool.NewResourceClaims);

        //    //put back 5 resources ( the first one should be released as capacity is exceeded)
        //    pool.Put(res1);
        //    pool.Put(res2);
        //    pool.Put(res3);
        //    pool.Put(res4);
        //    pool.Put(res5);

        //    Assert.AreEqual(pool.ResourcesInPool, 4);
        //    //res1 was released, the next returned one should be res2
        //    var res2Again = pool.Get();
        //    Assert.AreSame(res2, res2Again);
        //}

        [Test]
        public void UnreliableResourceProvider()
        {
            using var pool = new UnreliableProviderPool(4);

            Assert.IsTrue(pool.NewResourceClaims <= 3);

            var validCount = 0;
            var nonNull = 0;
            for (var i = 0; i < 5000; i++)
            {
                var resource = pool.Get();


                if (resource != null)
                {
                    nonNull++;

                    if (resource.IsValid)
                    {
                        resource.Use();
                        validCount++;

                        pool.Put(resource);
                    }
                }
            }


            Assert.IsTrue(validCount == nonNull && nonNull < 5000);
            Assert.IsTrue(validCount > 1000); //around 90% of the resources should be valid

            Assert.IsTrue(pool.NewResourceClaims > 3);

            pool.ClearAll();
        }
    }
}