using Channel;
using System;
using System.Threading;

namespace Tests.UnitTests
{
    /// <summary>
    ///     Simulates a provider which is sometimes unavailable
    /// </summary>
    internal class UnreliableProviderPool : PoolStrategy<ExpiryResource>
    {
        private readonly Random _randomGen = new Random();
        private long _newResourceClaims;

        public UnreliableProviderPool(int poolCapacity)
            : base(poolCapacity)
        {
        }



        public long NewResourceClaims => Interlocked.Read(ref _newResourceClaims);

        protected override ExpiryResource GetShinyNewResource()
        {
            Interlocked.Increment(ref _newResourceClaims);
            lock (_randomGen)
            {
                if (_randomGen.Next(0, 100) < 10)
                    return null;
                return new ExpiryResource();
            }
        }

        protected override bool IsStillValid(ExpiryResource resource)
        {
            if (resource == null)
                return false;

            return resource.IsValid;
        }

        protected override void Release(ExpiryResource resource)
        {
            //nothing to do
        }
    }
}