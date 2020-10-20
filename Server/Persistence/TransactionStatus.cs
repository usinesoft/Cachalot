namespace Server.Persistence
{
    public enum TransactionStatus
    {
        ToProcess,
        Processing,
        Processed,
        Canceled
    }
}