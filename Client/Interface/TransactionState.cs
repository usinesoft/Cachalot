using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.ChannelInterface;
using Client.Core;
using Client.Messages;
using Client.Queries;
using Client.Tools;

namespace Client.Interface
{

    /// <summary>
    /// State machine mor two stage transactions
    /// </summary>
    public partial class DataAggregator
    {
        private partial class TransactionState
        {
            public enum Status
            {
                None,
                Initialized,
                RunningAsSingleStage,
                AcquiringLocks,
                LocksAcquired,
                RunningFirstStage,
                FirstStageCompleted,
                RunningSecondStage,
                SecondStageCompleted,
                Completed,
                Failed
            }

            public Status CurrentStatus { get; private set; } = Status.None;

            public TransactionState()
            {
                TransactionId = Guid.NewGuid();
            }

            private int WhichNode(CachedObject item)
            {
                return item.PrimaryKey.GetHashCode() % Shards;
            }

            public void CheckStatus(Status requiredStatus)
            {
                if (CurrentStatus != requiredStatus)
                {
                    throw new NotSupportedException($"Current status should be {requiredStatus} but it is {CurrentStatus} ");
                }
            }

            public void Initialize(IList<CachedObject> itemsToPut, IList<OrQuery> conditions,
                IList<CachedObject> itemsToDelete, IList<DataClient> clients)
            {
                CheckStatus(Status.None);

                Shards = clients.Count;

                var index = 0;
                foreach (var item in itemsToPut)
                {
                    var serverIndex = WhichNode(item);

                    var request = RequestByServer.GetOrCreate(serverIndex);

                    request.ItemsToPut.Add(item);
                    request.Conditions.Add(conditions[index]);
                    request.TransactionId = TransactionId;


                    index++;
                }

                if (itemsToDelete != null)
                    foreach (var item in itemsToDelete)
                    {
                        var serverIndex = WhichNode(item);

                        var request = RequestByServer.GetOrCreate(serverIndex);

                        request.ItemsToDelete.Add(item);


                        index++;
                    }

                var usedClients = clients.Where(c => RequestByServer.ContainsKey(c.ShardIndex)).ToList();

                Clients.AddRange(usedClients);

                CurrentStatus = Status.Initialized;
            }

            public void TryExecuteAsSingleStage()
            {
                CheckStatus(Status.Initialized);

                if (IsSingleStage)
                {
                    var request = RequestByServer.Values.Single();
                    Clients.Single().ExecuteTransaction(request.ItemsToPut, request.Conditions, request.ItemsToDelete);

                    CurrentStatus = Status.Completed;
                }
                    
            }

            public void AcquireLock()
            {
                CheckStatus(Status.Initialized);

                CurrentStatus = Status.AcquiringLocks;
                
                LockPolicy.SmartRetry(TryAcquireLock);

                CurrentStatus = Status.LocksAcquired;

            }

            public void ProceedWithFirstStage()
            {
                CheckStatus(Status.LocksAcquired);

                // first stage: the durable transaction is written in the transaction log
                Parallel.ForEach(Clients, client =>
                {
                    try
                    {
                        var session = SessionByServer[client.ShardIndex];

                        var response = client.Channel.GetResponse(session);

                        if (response is ReadyResponse)
                        {
                            StatusByServer[client.ShardIndex] = true;
                        }
                        else
                        {
                            StatusByServer[client.ShardIndex] = false;

                            if (response is ExceptionResponse exceptionResponse)
                                ExceptionType = exceptionResponse.ExceptionType;
                        }
                    }
                    catch (Exception)
                    {
                        StatusByServer[client.ShardIndex] = false;
                    }
                });

                CurrentStatus = AllOk ? Status.FirstStageCompleted : Status.Failed;
            }

            public void Rollback()
            {
                CheckStatus(Status.Failed);

                Parallel.ForEach(Clients, client =>
                {
                    // need to rollback only the clients that have executed the first stage
                    if (StatusByServer[client.ShardIndex])
                    {
                        var session = SessionByServer[client.ShardIndex];
                        client.Channel.Continue(session, false);
                    }
                });

                
            }

            public void CommitSecondStage()
            {
                CheckStatus(Status.FirstStageCompleted);

                Parallel.ForEach(Clients, client =>
                {
                    var session = SessionByServer[client.ShardIndex];
                    client.Channel.Continue(session, true);
                });

                CurrentStatus = Status.SecondStageCompleted;
            }

            public void CloseTransaction()
            {
                CheckStatus(Status.SecondStageCompleted);

                // close the session. This will release the locks on the server
                Parallel.ForEach(Clients, client =>
                {
                    var session = SessionByServer[client.ShardIndex];
                    client.Channel.EndSession(session);
                }); 

                CurrentStatus = Status.Completed;
            }

            private bool TryAcquireLock()
            {
                try
                {

                    // send transaction requests
                    Parallel.ForEach(Clients, client =>
                    {
                        var request = RequestByServer[client.ShardIndex];

                        try
                        {
                            var session = client.Channel.BeginSession();
                            SessionByServer[client.ShardIndex] = session;

                            Dbg.Trace(
                                $"C: Sending transaction request to server {client.ShardIndex} transaction {TransactionId} connector {GetHashCode()}");
                            client.Channel.PushRequest(session, request);
                        }
                        catch (Exception e)
                        {
                            // here if communication exception
                            StatusByServer[client.ShardIndex] = false;

                            Dbg.Trace($"C: Exception while sending request to server {client.ShardIndex}:{e.Message}");
                        }
                    });

                    // wait for servers to acquire lock
                    Parallel.ForEach(Clients, client =>
                    {
                        try
                        {
                            var session = SessionByServer[client.ShardIndex];

                            var answer = client.Channel.GetResponse(session);
                            if (answer is ReadyResponse)
                                StatusByServer[client.ShardIndex] = true;
                            else
                                StatusByServer[client.ShardIndex] = false;
                        }
                        catch (Exception e)
                        {
                            // here if communication exception
                            StatusByServer[client.ShardIndex] = false;

                            Dbg.Trace($"C: Exception while sending request to server {client.ShardIndex}:{e.Message}");
                        }
                    });
                }
                catch (AggregateException e)
                {
                    // this code should never be reached
                    throw new CacheException(
                        $"Error in the first stage of a two stage transaction:{e.InnerExceptions.First()}");
                }

                var locksOk = AllOk;

                if (!locksOk) // some clients failed to acquire lock. Release locks on the ones who were successful
                    Parallel.ForEach(Clients, client =>
                    {
                        if (StatusByServer[client.ShardIndex])
                        {
                            var session = SessionByServer[client.ShardIndex];
                            client.Channel.PushRequest(session, new ContinueRequest {Rollback = true});
                        }
                    });
                else // all clients have acquired locks 
                    Parallel.ForEach(Clients, client =>
                    {
                        var session = SessionByServer[client.ShardIndex];
                        client.Channel.PushRequest(session, new ContinueRequest {Rollback = false});
                    });

                return locksOk;
            }

            private int Shards { get; set; }
            public SafeDictionary<int, Session> SessionByServer { get; } = new SafeDictionary<int, Session>(null);
            public SafeDictionary<int, bool> StatusByServer { get; } = new SafeDictionary<int, bool>(null);

            public SafeDictionary<int, TransactionRequest> RequestByServer { get; } =
                new SafeDictionary<int, TransactionRequest>(() => new TransactionRequest());

            public List<DataClient> Clients { get; } = new List<DataClient>();
            public Guid TransactionId { get; }
            public bool IsSingleStage => Clients.Count == 1;
            public bool AllOk => StatusByServer.Values.All(s => s);

            public ExceptionType ExceptionType { get; private set; }
        }
    }
}