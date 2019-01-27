namespace Client.Interface
{
    public enum ExceptionType
    {
        Unknown,
        FailedToAcquireLock,
        CommunicationErrorInTransaction,
        ConditionNotSatisfied,
        ErrorWritingDataInTransactionLog
    }
}