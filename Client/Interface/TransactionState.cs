using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.ChannelInterface;
using Client.Core;
using Client.Messages;
using Client.Tools;

namespace Client.Interface;

/// <summary>
///     State machine mor two stage transactions
/// </summary>
public sealed partial class DataAggregator
{
    private sealed class TransactionState
    {
        public enum Status
        {
            None,
            Initialized,
            AcquiringLocks,
            LocksAcquired,
            FirstStageCompleted,
            SecondStageCompleted,
            Completed,
            Failed
        }

        public TransactionState()
        {
            TransactionId = Guid.NewGuid();
        }

        public Status CurrentStatus { get; private set; } = Status.None;

        private int Shards { get; set; }
        public SafeDictionary<int, Session> SessionByServer { get; } = new(null);
        public SafeDictionary<int, bool> StatusByServer { get; } = new(null);

        public SafeDictionary<int, TransactionRequest> RequestByServer { get; set; } = new(() => new());

        public List<DataClient> Clients { get; set; } = new();
        public Guid TransactionId { get; }
        public bool IsSingleStage => Clients.Count == 1;
        public bool AllOk => StatusByServer.Values.All(s => s);

        public ExceptionType ExceptionType { get; private set; }

        private int WhichNode(KeyValue primaryKey)
        {
            return primaryKey.GetHashCode() % Shards;
        }

        public void CheckStatus(Status requiredStatus)
        {
            if (CurrentStatus != requiredStatus)
                throw new NotSupportedException(
                    $"Current status should be {requiredStatus} but it is {CurrentStatus} ");
        }

        public void Initialize(IList<DataRequest> requests, IList<DataClient> clients)
        {
            CheckStatus(Status.None);

            Shards = clients.Count;

            var transactionRequest = new TransactionRequest(requests) { TransactionId = Guid.NewGuid() };

            RequestByServer = transactionRequest.SplitByServer(WhichNode, Shards);

            var usedClients = clients.Where(c => RequestByServer.ContainsKey(c.ShardIndex)).ToList();

            Clients = usedClients;

            CurrentStatus = Status.Initialized;
        }

        public void TryExecuteAsSingleStage()
        {
            CheckStatus(Status.Initialized);

            if (IsSingleStage)
            {
                var request = RequestByServer.Values.Single();
                Clients.Single().ExecuteTransaction(request.ChildRequests);

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

            // close the session. The locks on the server have already been released
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
                // reserve sessions for all server to prevent a deadlock between the connection pool and the server-side locks
                foreach (var client in Clients)
                {
                    var session = client.Channel.BeginSession();
                    SessionByServer[client.ShardIndex] = session;
                }


                // send transaction requests
                Parallel.ForEach(Clients, client =>
                {
                    var request = RequestByServer[client.ShardIndex];

                    try
                    {
                        var session = SessionByServer[client.ShardIndex];

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
                    $"Error in the first stage of a two stage transaction:{e.InnerExceptions[0]}");
            }

            var locksOk = AllOk;

            if (!locksOk) // some clients failed to acquire lock. Release locks on the ones who were successful
                Parallel.ForEach(Clients, client =>
                {
                    if (StatusByServer[client.ShardIndex])
                    {
                        var session = SessionByServer[client.ShardIndex];
                        client.Channel.PushRequest(session, new ContinueRequest { Rollback = true });
                    }
                });
            else // all clients have acquired locks 
                Parallel.ForEach(Clients, client =>
                {
                    var session = SessionByServer[client.ShardIndex];
                    client.Channel.PushRequest(session, new ContinueRequest { Rollback = false });
                });

            return locksOk;
        }
    }
}