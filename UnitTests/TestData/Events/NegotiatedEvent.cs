namespace Tests.TestData.Events
{
    public abstract class NegotiatedEvent : Event
    {
        public abstract bool NeedsConfirmation { get; }
    }
}