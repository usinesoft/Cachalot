using System;
using Client.Core;
using Client.Interface;

namespace StressTests.Model
{
    /// <summary>
    ///     The quantity of a product available in an outlet
    /// </summary>
    public class Stock
    {
        [ServerSideValue(IndexType.Primary)] public Guid Id { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public Guid OutletId { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public Guid ProductId { get; set; }

        [ServerSideValue(IndexType.Ordered)] public decimal Quantity { get; set; }
    }
}