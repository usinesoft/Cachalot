using System;
using Client.Core;
using Client.Interface;

// ReSharper disable NonReadonlyMemberInGetHashCode

namespace Tests.TestData.Events
{
    public abstract class ProductEvent
    {
        [ServerSideValue(IndexType.Primary)] public int EventId { get; set; }

        [ServerSideValue(IndexType.Dictionary)] public abstract string EventType { get; }

        public string Comment { get; set; }

        [ServerSideValue(IndexType.Dictionary)] public string DealId { get; set; }


        [ServerSideValue(IndexType.Ordered)] public DateTime EventDate { get; set; }

        [ServerSideValue(IndexType.Ordered)] public DateTime ValueDate { get; set; }

        [ServerSideValue(IndexType.Ordered)] public DateTime Timestamp { get; set; }

        protected bool Equals(ProductEvent other)
        {
            return EventId == other.EventId;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ProductEvent) obj);
        }

        public override int GetHashCode()
        {
            return EventId;
        }

        public static bool operator ==(ProductEvent left, ProductEvent right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ProductEvent left, ProductEvent right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            return $"{nameof(EventId)}: {EventId}, {nameof(EventType)}: {EventType}";
        }
    }
}