using System;

namespace Tests.TestData.Events
{
    public class Create : NegotiatedEvent
    {
        public Create(int id, string dealId)
        {
            EventId = id;

            DealId = dealId;

            EventDate = DateTime.Today;
            ValueDate = DateTime.Today;
        }

        public override bool NeedsConfirmation => true;

        public override string EventType => "CREATE";
    }
}