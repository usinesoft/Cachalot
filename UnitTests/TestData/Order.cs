using Client.Core;
using Client.Interface;
using System;
using System.Collections.Generic;

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

        public static List<Order> GenerateTestData(int count)
        {
            var result = new List<Order>();

            var categories = new[] { "geek", "camping", "sf", "food", "games" };

            var rg = new Random(Environment.TickCount);

            var date = new DateTimeOffset(2020, 01, 04, 0, 0, 0, TimeSpan.Zero);

            for (var i = 0; i < count; i++)
            {
                result.Add(new Order
                {
                    Id = Guid.NewGuid(),
                    Amount = rg.NextDouble() * 100,
                    Category = categories[i % categories.Length],
                    ClientId = i % 100,
                    Date = date,
                    IsDelivered = i % 2 == 0,
                    ProductId = rg.Next(10, 100),
                    Quantity = rg.Next(1, 5)
                });

                date = date.AddHours(1);
            }


            return result;
        }
    }
}
