using System;

namespace Tests.TestData.Events
{
    public class Increase : NegotiatedEvent
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