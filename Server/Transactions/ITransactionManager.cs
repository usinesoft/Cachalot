using Client.ChannelInterface;
using Client.Messages;
using Client.Tools;

namespace Server.Transactions;

public interface ITransactionManager
{
    void ProcessTransactionRequest(TransactionRequest transactionRequest, IClient client,
                                   SafeDictionary<string, DataStore> dataStores);
}