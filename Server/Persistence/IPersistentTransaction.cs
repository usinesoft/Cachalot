using System;

namespace Server.Persistence
{
    public interface IPersistentTransaction
    {
        byte[] Data { get; }
        DateTime TimeStamp { get; }
        TransactionStaus TransactionStatus { get; }

        long Id { get; }
    }
}