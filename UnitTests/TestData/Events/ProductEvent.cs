using System;
using Client.Interface;

// ReSharper disable NonReadonlyMemberInGetHashCode

namespace UnitTests.TestData.Events
{
    public abstract class ProductEvent
    {
        [PrimaryKey(KeyDataType.IntKey)] public int EventId { get; set; }

        [Index(KeyDataType.StringKey)] public abstract string EventType { get; }

        public string Comment { get; set; }

        [Index(KeyDataType.StringKey)] public string DealId { get; set; }


        [Index(KeyDataType.IntKey, true)] public DateTime EventDate { get; set; }

        [Index(KeyDataType.IntKey, true)] public DateTime ValueDate { get; set; }

        [Index(KeyDataType.IntKey, true)] public DateTime Timestamp { get; set; }

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