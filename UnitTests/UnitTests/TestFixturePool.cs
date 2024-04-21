using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
            ClassicAssert.AreEqual(1, pool.NewResourceClaims);

            //this one too
            var res2 = pool.Get();
            ClassicAssert.AreEqual(2, pool.NewResourceClaims);

            //now we put a resource back to the pool
            pool.Put(res2);

            //this call should be served with the pooled resource
            _ = pool.Get();
            ClassicAssert.AreEqual(2, pool.NewResourceClaims);
        }

        [Test]
        public void DoNotClaimUnlimitedNewResources()
        {
            var pool = new SlowProviderPool(4);

            long count = 0;

            Parallel.For(0, 10, _ =>
            {
                var res = pool.Get();
                
                pool.Put(res);
                
                // ReSharper disable once AccessToModifiedClosure
                Interlocked.Increment(ref count);
            });

            

            var claimCount = pool.NewResourceClaims;

            //as consumer threads are much faster than the resource provider, most of the connections
            //are recycled ones not new ones
            ClassicAssert.IsTrue(claimCount <= pool.MaxPendingClaims);
        }

        [Test]
        public void PreloadedResourcePool()
        {
            var pool = new SlowProviderPool(4, 3);

            ClassicAssert.AreEqual(3, pool.NewResourceClaims);

            ClassicAssert.AreEqual(3, pool.ResourcesInPool);

            //three requests should be served by the pool without claiming external resource
            var res1 = pool.Get();
            var res2 = pool.Get();
            var res3 = pool.Get();

            ClassicAssert.AreEqual(3, pool.NewResourceClaims, 3);

            //this one is claimed from outside
            var res4 = pool.Get();
            ClassicAssert.AreEqual(4, pool.NewResourceClaims);

            //this one too
            var res5 = pool.Get();
            ClassicAssert.AreEqual(5, pool.NewResourceClaims);

            //put back 5 resources ( the first one should be released as capacity is exceeded)
            pool.Put(res1);
            pool.Put(res2);
            pool.Put(res3);
            pool.Put(res4);
            pool.Put(res5);

            ClassicAssert.AreEqual(4, pool.ResourcesInPool, 4);
            //res1 was released, the next returned one should be res2
            var res2Again = pool.Get();
            ClassicAssert.AreSame(res2, res2Again);
        }

        [Test]
        public void UnreliableResourceProvider()
        {
            using var pool = new UnreliableProviderPool(4);

            ClassicAssert.IsTrue(pool.NewResourceClaims <= 3);

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


            ClassicAssert.IsTrue(validCount == nonNull && nonNull < 5000);
            ClassicAssert.IsTrue(validCount > 1000); //around 90% of the resources should be valid

            ClassicAssert.IsTrue(pool.NewResourceClaims > 3);

            pool.ClearAll();
        }
    }
}