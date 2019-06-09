using System;

namespace UnitTests.TestData.Events
{
    public class Increase : NegotiatedProductEvent
    {
        public Increase(int id, decimal delta, string dealId)
        {
            EventId = id;
            Delta = delta;

            DealId = dealId;

            EventDate = DateTime.Today;
            ValueDate = DateTime.Today;
        }

        public decimal Delta { get; set; }

        public override bool NeedsConfirmation => true;

        public override string EventType => "INCREASE";
    }
}