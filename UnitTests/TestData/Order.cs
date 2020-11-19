using System;
using Client.Interface;

namespace UnitTests.TestData
{
    public class Order
    {

        [PrimaryKey]
        public Guid Id { get; set; }

        [ServerSideVisible]
        [Index(ordered:true)]
        public double Amount { get; set; }

        [ServerSideVisible]
        public int Quantity { get; set; }

        [Index]
        public string Category { get; set; }

        [Index]
        public int ProductId { get; set; }

        [Index]
        public int ClientId { get; set; }

        [Index]
        public DateTimeOffset Date { get; set; }

        [Index]
        public DayOfWeek DayOfWeek => Date.DayOfWeek;

        [Index]
        public int Month => Date.Month;
        
        [Index]
        public int Year => Date.Year;
    }
}
