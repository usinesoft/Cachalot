using System;
using Client.Core;
using Client.Interface;

namespace Tests.TestData
{
    public class Order
    {

        [ServerSideValue(IndexType.Primary)]
        public Guid Id { get; set; }

        [ServerSideValue(IndexType.Ordered)]
        public double Amount { get; set; }

        [ServerSideValue()]
        public int Quantity { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public string Category { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public int ProductId { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public int ClientId { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public DateTimeOffset Date { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public DayOfWeek DayOfWeek => Date.DayOfWeek;

        [ServerSideValue(IndexType.Dictionary)]
        public int Month => Date.Month;
        
        [ServerSideValue(IndexType.Dictionary)]
        public int Year => Date.Year;

        [ServerSideValue(IndexType.Dictionary)]
        public bool IsDelivered { get; set; }
    }
}
