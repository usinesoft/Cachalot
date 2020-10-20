using System;

namespace Server.Persistence
{
    public interface IPersistentTransaction
    {
        byte[] Data { get; }
        DateTime TimeStamp { get; }
        TransactionStatus TransactionStatus { get; }

        long Id { get; }
    }
}