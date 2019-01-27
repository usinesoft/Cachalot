using System;

namespace UnitTests.TestData.Events
{
    public class Increase : NegotiatedProductEvent
    {
        public Decimal Delta { get; set; }

        public override bool NeedsConfirmation => true;

        public override string EventType => "INCREASE";

        public Increase(int id, decimal delta, string dealId)
        {

            EventId = id;
            Delta = delta;

            DealId = dealId;

            EventDate = DateTime.Today;
            ValueDate = DateTime.Today;
            
        }
    }
}