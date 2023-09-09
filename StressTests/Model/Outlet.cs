using System;
using Client.Core;
using Client.Interface;

namespace StressTests.Model
{
    public class Outlet
    {
        [ServerSideValue(IndexType.Primary)] public Guid Id { get; set; }

        [FullTextIndexation]
        [ServerSideValue(IndexType.Dictionary)]
        public string Name { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public string Country { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public string Town { get; set; }

        [FullTextIndexation]
        [ServerSideValue(IndexType.Dictionary)]
        public string Address { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public string Currency { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public bool Active { get; set; }

        [FullTextIndexation]
        [ServerSideValue(IndexType.Dictionary)]
        public string ContactName { get; set; }
    }
}