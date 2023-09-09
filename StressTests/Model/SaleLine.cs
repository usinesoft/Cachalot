using System;
using Client.Core;
using Client.Interface;

namespace StressTests.Model
{
    public enum Channel
    {
        Direct,
        Facebook,
        Web
    }

    /// <summary>
    ///     One product in a sale operation
    /// </summary>
    public class SaleLine
    {
        [ServerSideValue(IndexType.Primary)] public Guid Id { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public Guid SaleId { get; set; }

        [ServerSideValue(IndexType.Ordered)] public double Amount { get; set; }

        [ServerSideValue] public int Quantity { get; set; }


        [ServerSideValue(IndexType.Dictionary)]
        public int ProductId { get; set; }

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

        [ServerSideValue(IndexType.Dictionary)]
        public bool IsDelivered { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public Channel Channel { get; set; }

        protected bool Equals(SaleLine other)
        {
            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((SaleLine)obj);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}