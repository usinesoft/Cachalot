using System;
using System.Linq;
using Client.ChannelInterface;
using Client.Messages;
using Client.Queries;
using Server.Persistence;

namespace Server.Queries;

internal class DeleteManager : IRequestManager
{
    private readonly DataStore _dataStore;
    private readonly ITransactionLog _transactionLog;

    public DeleteManager(DataStore dataStore, ITransactionLog transactionLog)
    {
        _dataStore = dataStore;
        _transactionLog = transactionLog;
    }

    public void ProcessRequest(Request request, IClient client)
    {
        if (request is RemoveManyRequest removeManyRequest)
        {
            try
            {
                var removed = RemoveMany(removeManyRequest.Query);


                client?.SendResponse(new ItemsCountResponse { ItemsCount = removed });
            }
            catch (Exception e)
            {
                client?.SendResponse(new ExceptionResponse(e));

                // if client is null we are inside a transaction. The exception will be processed at a higher level
                if (client == null) throw;
            }

            return;
        }

        if (request is RemoveRequest removeRequest) // remove one item by primary key
        {
            var removed = _dataStore.RemoveByPrimaryKey(removeRequest.PrimaryKey);

            if (removed != null)
                _transactionLog?.NewTransaction(new DeleteDurableTransaction
                {
                    GlobalKeysToDelete = { removed.GlobalKey }
                });

            return;
        }

        throw new NotSupportedException($"Can not process this request type:{request.GetType().Name}");
    }

    private int RemoveMany(OrQuery query)
    {
        if (query.IsEmpty()) // an empty query means truncate the table
        {
            var all = _dataStore.DataByPrimaryKey.Values.ToList();

            _transactionLog?.NewTransaction(new DeleteDurableTransaction
            {
                GlobalKeysToDelete = all.Select(x => x.GlobalKey).ToList()
            });
            ;

            var count = all.Count;
            _dataStore.Truncate();

            return count;
        }

        var queryManager = new QueryManager(_dataStore);

        var toRemove = queryManager.ProcessQuery(query);

        _transactionLog?.NewTransaction(new DeleteDurableTransaction
        {
            GlobalKeysToDelete = toRemove.Select(x => x.GlobalKey).ToList()
        });

        _dataStore.RemoveMany(toRemove);

        return toRemove.Count;
    }
}