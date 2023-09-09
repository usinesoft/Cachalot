namespace Server.Persistence;

public interface ITransactionLog
{
    void NewTransaction(DurableTransaction durableTransaction, bool isDelayed = false);
    void CancelDelayedTransaction();
}