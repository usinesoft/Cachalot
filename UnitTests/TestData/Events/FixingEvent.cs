using System;

namespace UnitTests.TestData.Events
{
    public class FixingEvent:ProductEvent
    {
        public string Underlying { get; set; }

        public Decimal Value { get; set; }

        public DateTime FixingDate { get; set; }

        public override string EventType => "FIXING";


        public FixingEvent(int id, string underlying, decimal value, string dealId)
        {
            EventId = id;
            Underlying = underlying;
            Value = value;

            DealId = dealId;

            FixingDate = DateTime.Today;
            EventDate = DateTime.Today;
            ValueDate = DateTime.Today;
        }
    }
}