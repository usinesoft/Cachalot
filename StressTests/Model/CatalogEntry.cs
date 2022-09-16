using Client.Core;
using Client.Interface;
using System;

namespace StressTests.Model
{
    /// <summary>
    /// A product available in an outlet at a price
    /// </summary>
    public class CatalogEntry
    {
        [ServerSideValue(IndexType.Primary)]
        public Guid Id { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public Guid OutletId { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public Guid ProductId { get; set; }

        [ServerSideValue(IndexType.Ordered)]
        public decimal UnitPrice { get; set; }


    }
}