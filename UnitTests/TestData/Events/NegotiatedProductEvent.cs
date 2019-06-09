namespace UnitTests.TestData.Events
{
    public abstract class NegotiatedProductEvent : ProductEvent
    {
        public abstract bool NeedsConfirmation { get; }
    }
}