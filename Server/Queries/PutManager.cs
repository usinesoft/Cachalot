using Client;
using Client.ChannelInterface;
using Client.Core;
using Client.Interface;
using Client.Messages;
using Server.Persistence;
using System;
using System.Diagnostics;
using System.Linq;

namespace Server.Queries
{
    /// <summary>
    /// Processes write requests (put) 
    /// </summary>
    class PutManager : IRequestManager
    {

        private readonly DataStore _dataStore;
        private readonly ILog _log;
        private readonly ITransactionLog _transactionLog;

        public PutManager(ITransactionLog transactionLog, IFeedSessionManager sessionManager, DataStore dataStore, ILog log)
        {
            _transactionLog = transactionLog;
            SessionManager = sessionManager;
            _dataStore = dataStore;
            _log = log;
        }

        public IFeedSessionManager SessionManager { get; }

        public void ProcessRequest(Request request, IClient client)
        {
            if (request is PutRequest putRequest)
            {
                try
                {
                    var watch = new Stopwatch();


                    watch.Start();
                    int count = ProcessPutRequest(putRequest);
                    watch.Stop();

                    _log?.LogActivity(LogEntry.Put, putRequest.CollectionName, (int)(watch.Elapsed.TotalMilliseconds * 1000), $"{putRequest.Items.Count} items");

                    client?.SendResponse(new ItemsCountResponse { ItemsCount = count });
                }
                catch (Exception e)
                {
                    client?.SendResponse(new ExceptionResponse(e));

                    // if client is null we are inside a transaction. The exception will be processed at a higher level
                    if (client == null)
                    {
                        throw;
                    }
                }
                finally
                {
                    _dataStore.ProcessEviction();
                }

                return;
            }

            throw new ArgumentException($"Request type not supported:{request.GetType()} ");

        }

        private int ProcessPutRequest(PutRequest putRequest)
        {

            Dbg.Trace($"Put request {putRequest.Items.Count:D5} items put session{putRequest.SessionId}");

            // a feed session may contain multiple requests
            if (putRequest.SessionId != default)
            {

                KeyValuePool.ProcessPackedObjects(putRequest.Items);

                
                SessionManager.AddToSession(putRequest.SessionId, putRequest.Items);

                if (putRequest.EndOfSession)
                {
                    var all = SessionManager.EndSession(putRequest.SessionId);

                    _transactionLog?.NewTransaction(new PutDurableTransaction { Items = all });

                    _dataStore.InternalPutMany(all, putRequest.ExcludeFromEviction);

                    GC.Collect();

                    Dbg.Trace($"End of session {all.Count:D5} items put session{putRequest.SessionId}");

                    return all.Count;



                }

                return 0;
            }

            if (putRequest.OnlyIfNew) // conditional insert
            {
                if (putRequest.Items.Count != 1)
                    throw new NotSupportedException("TryAdd can be called only with exactly one item");


                var item = putRequest.Items.First();
                if (!_dataStore.DataByPrimaryKey.ContainsKey(item.PrimaryKey))
                {
                    _transactionLog?.NewTransaction(new PutDurableTransaction { Items = putRequest.Items });

                    _dataStore.InternalAddNew(item, putRequest.ExcludeFromEviction);

                    return 1;
                }

                return 0;
            }

            if (putRequest.Predicate != null) // conditional update
            {
                if (putRequest.Items.Count != 1)
                    throw new NotSupportedException("UpdateIf can be called only with exactly one item");

                var newVersion = putRequest.Items.First();

                if (!_dataStore.DataByPrimaryKey.TryGetValue(newVersion.PrimaryKey, out var oldVersion))
                {
                    throw new NotSupportedException("Item not found. Conditional update failed");
                }

                if (putRequest.Predicate.Match(oldVersion))
                {
                    _transactionLog?.NewTransaction(new PutDurableTransaction { Items = putRequest.Items });

                    _dataStore.InternalUpdate(newVersion);

                    return 1;
                }

                throw new CacheException("Condition not satisfied.Item not updated");

            }

            _transactionLog?.NewTransaction(new PutDurableTransaction { Items = putRequest.Items });

            _dataStore.InternalPutMany(putRequest.Items, putRequest.ExcludeFromEviction);

            return putRequest.Items.Count;
        }
    }
}