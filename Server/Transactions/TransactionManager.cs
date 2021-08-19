using System;
using System.Collections.Generic;
using System.Linq;
using Client;
using Client.ChannelInterface;
using Client.Core;
using Client.Interface;
using Client.Messages;
using Client.Tools;
using Server.Persistence;
using Server.Queries;
using Constants = Server.Persistence.Constants;

namespace Server.Transactions
{
    public class TransactionManager : ITransactionManager
    {
        private readonly ILockManager _lockManager;

        private readonly ITransactionLog _transactionLog;

        public TransactionManager(ILockManager lockManager, ITransactionLog transactionLog)
        {
            _lockManager = lockManager;
            _transactionLog = transactionLog;
        }

        private void ExecuteInMemory(TransactionRequest transactionRequest,
            SafeDictionary<string, DataStore> dataStores)
        {
            foreach (var dataRequest in transactionRequest.ChildRequests)
            {
                if (!dataStores.Keys.Contains(dataRequest.CollectionName))
                    throw new NotSupportedException(
                        $"The type {dataRequest.CollectionName} is not registered");

                var store = dataStores[dataRequest.CollectionName];

                switch (dataRequest)
                {
                    case PutRequest putRequest:
                        new PutManager(null, null, store, null).ProcessRequest(putRequest, null);
                        break;
                    case RemoveRequest  removeRequest:
                        new DeleteManager(store, null).ProcessRequest(removeRequest, null);
                        break;
                    case RemoveManyRequest removeManyRequest:
                        new DeleteManager(store, null).ProcessRequest(removeManyRequest, null);
                        break;
                    default:
                        throw new NotSupportedException($"Invalid request type in transaction: {dataRequest.GetType().Name}");
                }

                //store.ProcessRequest(dataRequest, client, null);
            }
        }

        private void SaveDurableTransaction(TransactionRequest transactionRequest,
            SafeDictionary<string, DataStore> dataStores)
        {
            // get the items to delete from the DeleteManyRequests
            var itemsToDelete = new HashSet<PackedObject>();

            var deleteManyRequests = transactionRequest.ChildRequests.Where(r => r is RemoveManyRequest)
                .Cast<RemoveManyRequest>().ToList();

            foreach (var deleteManyRequest in deleteManyRequests)
            {
                var ds = dataStores[deleteManyRequest.CollectionName];
                var items = new QueryManager(ds).ProcessQuery(deleteManyRequest.Query);
                
                foreach (var item in items) itemsToDelete.Add(item);
            }

            // add items to delete explicitly
            foreach (var remove in transactionRequest.ChildRequests.Where(r => r is RemoveRequest)
                .Cast<RemoveRequest>())
            {
                var collectionName = remove.CollectionName;

                if (dataStores[collectionName].DataByPrimaryKey.TryGetValue(remove.PrimaryKey, out var item))
                {
                    itemsToDelete.Add(item);
                }
                
            }

            // get items to put (conditions have already been checked)
            var itemsToPut = transactionRequest.ChildRequests.Where(r => r is PutRequest).Cast<PutRequest>()
                .SelectMany(r => r.Items).ToList();

            Dbg.Trace($"S: begin writing delayed transaction {transactionRequest.TransactionId}");
            _transactionLog?.NewTransaction(new MixedDurableTransaction
                {
                    ItemsToDelete = itemsToDelete.ToList(),
                    ItemsToPut = itemsToPut
                },
                true
            );
        }

        private void CheckConditions(TransactionRequest transactionRequest,
            SafeDictionary<string, DataStore> dataStores)
        {
            // check the conditions (in case of conditional update)
            foreach (var conditionalRequest in transactionRequest.ConditionalRequests)
            {
                var ds = dataStores[conditionalRequest.CollectionName];

                var primaryKey = conditionalRequest.Items.Single().PrimaryKey;
                if (ds.DataByPrimaryKey.TryGetValue(primaryKey, out var item))
                {
                    if (!conditionalRequest.Predicate.Match(item))
                        throw new CacheException(
                            $"Condition not satisfied for item {primaryKey} of type {ds.CollectionSchema.CollectionName}",
                            ExceptionType.ConditionNotSatisfied);
                }
                else
                {
                    throw new CacheException($"Item {primaryKey} of type {ds.CollectionSchema.CollectionName} not found");
                }

            }
        }

        private void ProcessSingleStageTransactionRequest(TransactionRequest transactionRequest, IClient client,
            SafeDictionary<string, DataStore> dataStores)
        {
            var types = transactionRequest.AllCollections;


            _lockManager.DoWithWriteLock(() =>
            {
                try
                {
                    Dbg.Trace($"S: fallback to single stage for transaction {transactionRequest.TransactionId}");


                    // check the conditions (in case of conditional update)
                    CheckConditions(transactionRequest, dataStores);

                    SaveDurableTransaction(transactionRequest, dataStores);

                    // update the data in memory
                    ExecuteInMemory(transactionRequest, dataStores);

                    client.SendResponse(new NullResponse());

                    Dbg.Trace($"S: end writing delayed transaction {transactionRequest.TransactionId}");
                }
                catch (CacheException e)
                {
                    client.SendResponse(new ExceptionResponse(e, e.ExceptionType));
                }
                catch (Exception e)
                {
                    Dbg.Trace($"error writing delayed transaction:{e.Message}");
                    client.SendResponse(new ExceptionResponse(e));
                    // failed to write a durable transaction so stop here
                }
            }, types);
        }

        public void ProcessTransactionRequest(TransactionRequest transactionRequest, IClient client,
            SafeDictionary<string, DataStore> dataStores)
        {
           

            if (transactionRequest.TransactionId == default)
            {
                client.SendResponse(new ExceptionResponse(
                    new CacheException(
                        "Transaction request received without transaction id")));
                return;
            }


            // First try to acquire a write lock on all concerned data stores
            var types = transactionRequest.AllCollections;


            var keys = dataStores.Keys;

            if (types.Any(t => !keys.Contains(t))) throw new NotSupportedException("Type not registered");

            var transactionId = transactionRequest.TransactionId;


            // do not work too hard if it's single stage
            if (transactionRequest.IsSingleStage)
            {
                ProcessSingleStageTransactionRequest(transactionRequest, client, dataStores);
                return;
            }

            var lockAcquired = _lockManager.TryAcquireWriteLock(transactionId, Constants.DelayForLockInMilliseconds, types.ToArray());

            if (lockAcquired)
            {
                var answer = client.ShouldContinue();
                if (answer.HasValue && answer.Value)
                {
                }
                else
                {
                    _lockManager.CloseSession(transactionId);

                    return;
                }
            }
            else
            {
                client.SendResponse(new ExceptionResponse(
                    new CacheException(
                        $"can not acquire write lock on server for transaction {transactionRequest.TransactionId}"),
                    ExceptionType.FailedToAcquireLock));

                return;
            }


            Dbg.Trace($"S: lock acquired by all clients for transaction {transactionRequest.TransactionId}");


            // Second register a durable delayed transaction. It can be cancelled later

            try
            {
                CheckConditions(transactionRequest, dataStores);

                // if we reach here the condition check has passed

                SaveDurableTransaction(transactionRequest, dataStores);

                client.SendResponse(new ReadyResponse());


                Dbg.Trace($"S: end writing delayed transaction {transactionRequest.TransactionId}");
            }
            catch (CacheException e)
            {
                Dbg.Trace($"error in first stage:{e.Message} ");
                client.SendResponse(new ExceptionResponse(e, e.ExceptionType));
                // failed to write a durable transaction so stop here

                // unlock
                _lockManager.CloseSession(transactionRequest.TransactionId);

                return;
            }
            catch (Exception e)
            {
                Dbg.Trace($"error in first stage:{e.Message} ");
                client.SendResponse(new ExceptionResponse(e));
                // failed to write a durable transaction so stop here

                // unlock
                _lockManager.CloseSession(transactionRequest.TransactionId);

                return;
            }


            try
            {
                Dbg.Trace($"S: begin waiting for client go {transactionRequest.TransactionId}");
                var answer = client.ShouldContinue();
                Dbg.Trace($"S: end waiting for client go answer = {answer}");

                if (answer.HasValue) // the client has answered
                {
                    if (answer.Value)
                    {
                        // update the data in memory

                        ExecuteInMemory(transactionRequest, dataStores);

                        ServerLog.LogInfo(
                            $"S: two stage transaction committed successfully  {transactionRequest.TransactionId}");
                    }
                    else
                    {
                        ServerLog.LogWarning(
                            $"S: two stage transaction cancelled by client on server {transactionRequest.TransactionId}");

                        // cancel the delayed transaction
                        _transactionLog?.CancelDelayedTransaction();
                    }
                }
                else // the client failed to answer in a reasonable delay (which is less than the delay to commit a delayed transaction )
                {
                    _transactionLog?.CancelDelayedTransaction();
                }
            }
            catch (Exception e)
            {
                ServerLog.LogInfo($"error in the second stage of a transaction:{e.Message}");
            }


            // unlock
            _lockManager.CloseSession(transactionRequest.TransactionId);
        }
    }
}