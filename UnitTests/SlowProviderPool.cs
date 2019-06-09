using System.Threading;
using Channel;

namespace UnitTests
{
    /// <summary>
    ///     Simulates a resource pool connected to a slow provider provider
    ///     In this case, resource reciclying inside the pool should prevail
    /// </summary>
    internal class SlowProviderPool : PoolStrategy<SomeResource>
    {
        private long _newResourceClaims;

        public SlowProviderPool(int poolCapacity) : base(poolCapacity)
        {
        }


        public SlowProviderPool(int poolCapacity, int preloaded) : base(poolCapacity, preloaded)
        {
        }

        public long NewResourceClaims => Interlocked.Read(ref _newResourceClaims);

        protected override SomeResource GetShinyNewResource()
        {
            Interlocked.Increment(ref _newResourceClaims);
            Thread.Sleep(500);
            return new SomeResource();
        }

        protected override bool IsStillValid(SomeResource resource)
        {
            return resource.IsValid;
        }

        protected override void Release(SomeResource resource)
        {
            //nothing to do
        }
    }
}