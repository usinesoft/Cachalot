using Client.Core;
using Client.Interface;
using System;

namespace StressTests.Model
{
    /// <summary>
    /// Multiple items sold at once to the same client
    /// </summary>
    public class Sale
    {
        [ServerSideValue(IndexType.Primary)]
        public Guid Id { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public Guid OutletId { get; set; }

        [ServerSideValue(IndexType.Ordered)]
        public double Amount { get; set; }

        [FullTextIndexation]
        public string Comment { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public int ClientId { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public DateTime Date { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public DayOfWeek DayOfWeek => Date.DayOfWeek;

        [ServerSideValue(IndexType.Dictionary)]
        public int Month => Date.Month;

        [ServerSideValue(IndexType.Dictionary)]
        public int Year => Date.Year;
    }
}