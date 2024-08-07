﻿//#define DEBUG_VERBOSE

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Cachalot.Linq;
using Channel;
using Client;
using Client.Core;
using Client.Interface;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Server;
using Tests.TestData;
using Tests.TestData.MoneyTransfer;

// ReSharper disable AccessToDisposedClosure

// ReSharper disable AccessToModifiedClosure

namespace Tests.IntegrationTests
{
    [TestFixture]
    public class TestFixtureTwoStageTransactionsOnMultiServerCluster
    {
        [TearDown]
        public void Exit()
        {
            StopServers();

            // deactivate all failure simulations
            Dbg.DeactivateSimulation();
        }

        [SetUp]
        public void Init()
        {
            for (var i = 0; i < Servers; i++)
                if (Directory.Exists($"server{i:D2}"))
                    Directory.Delete($"server{i:D2}", true);


            StartServers(Servers);
        }

        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            Environment.CurrentDirectory = TestContext.CurrentContext.TestDirectory;
            Directory.SetCurrentDirectory(TestContext.CurrentContext.TestDirectory);
        }

        public int Servers { set; get; } = 3;
        public int Threads { set; get; } = 20;

        private class ServerInfo
        {
            public TcpServerChannel Channel { get; set; }
            public Server.Server Server { get; set; }
            public int Port { get; set; }
        }

        private List<ServerInfo> _servers = new List<ServerInfo>();

        private const int DefaultServerCount = 3;

        private void StopServers()
        {
            foreach (var serverInfo in _servers)
            {
                serverInfo.Channel.Stop();
                serverInfo.Server.Stop();
            }
        }

        private void TraceBegin([CallerMemberName] string method = null)
        {
            Console.WriteLine($"STARTING TEST {method}");
            Console.WriteLine();
        }

        private void TraceEnd([CallerMemberName] string method = null)
        {
            Console.WriteLine();
            Console.WriteLine($"FINISHED TEST {method}");
        }


        private ClientConfig _clientConfig;

        private void StartServers(int serverCount = 0)
        {
            _clientConfig = new ClientConfig();
            _servers = new List<ServerInfo>();

            serverCount = serverCount == 0 ? DefaultServerCount : serverCount;

            for (var i = 0; i < serverCount; i++)
            {
                var serverInfo = new ServerInfo { Channel = new TcpServerChannel() };
                var nodeConfig = new NodeConfig { IsPersistent = true, DataPath = $"server{i:D2}" };

                serverInfo.Server =
                    new Server.Server(nodeConfig) { Channel = serverInfo.Channel };

                serverInfo.Port = serverInfo.Channel.Init();
                serverInfo.Channel.Start();
                serverInfo.Server.Start();

                _servers.Add(serverInfo);

                _clientConfig.Servers.Add(
                    new ServerConfig { Host = "localhost", Port = serverInfo.Port });
            }

            _clientConfig.ConnectionPoolCapacity = 10;
            _clientConfig.PreloadedConnections = 10;


            Thread.Sleep(500); //be sure the server nodes are started
        }

        [Test]
        public void Conditional_update_in_transaction()
        {
            TraceBegin();
            int[] accountIds = null;

            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Account>();
                connector.DeclareCollection<MoneyTransfer>();

                var accounts = connector.DataSource<Account>();

                accountIds = connector.GenerateUniqueIds("account_id", 2);

                var tIds = connector.GenerateUniqueIds("transfer_ids", 2);

                accounts.Put(new Account { Id = accountIds[0], Balance = 1000 });
                accounts.Put(new Account { Id = accountIds[1], Balance = 0 });


                // first transaction should succeed

                {
                    var transferredMoney = 334;

                    var transaction = connector.BeginTransaction();
                    transaction.UpdateIf(new Account { Id = accountIds[0], Balance = 1000 - transferredMoney },
                        account => account.Balance >= transferredMoney);

                    transaction.Put(new Account { Id = accountIds[1], Balance = transferredMoney });
                    transaction.Put(new MoneyTransfer
                    {
                        Id = tIds[0],
                        Amount = transferredMoney,
                        SourceAccount = accountIds[0],
                        DestinationAccount = accountIds[1]
                    });
                    transaction.Commit();


                    // check that everything is updated in memory
                    var src = accounts[accountIds[0]];
                    var dst = accounts[accountIds[1]];

                    ClassicAssert.AreEqual(666, src.Balance);
                    ClassicAssert.AreEqual(334, dst.Balance);

                    var transfers = connector.DataSource<MoneyTransfer>();

                    var transfer = transfers.Single();
                    ClassicAssert.AreEqual(334, transfer.Amount);

                    ClassicAssert.AreEqual(334, transfer.Amount);
                }


                // second transaction should fail
                {
                    var transferredMoney = 1001;

                    var transaction = connector.BeginTransaction();
                    transaction.UpdateIf(new Account { Id = accountIds[0], Balance = 1000 - transferredMoney },
                        account => account.Balance >= transferredMoney);

                    transaction.Put(new Account { Id = accountIds[1], Balance = transferredMoney });
                    transaction.Put(new MoneyTransfer
                    {
                        Id = tIds[0],
                        Amount = transferredMoney,
                        SourceAccount = accountIds[0],
                        DestinationAccount = accountIds[1]
                    });


                    try
                    {
                        transaction.Commit();

                        Assert.Fail("Should have thrown an exception");
                    }
                    catch (CacheException e)
                    {
                        ClassicAssert.IsTrue(e.IsTransactionException);
                        ClassicAssert.AreEqual(ExceptionType.ConditionNotSatisfied, e.ExceptionType);

                        Console.WriteLine(e);
                    }


                    // check that nothing is updated in memory
                    var src = accounts[accountIds[0]];
                    var dst = accounts[accountIds[1]];

                    ClassicAssert.AreEqual(666, src.Balance);
                    ClassicAssert.AreEqual(334, dst.Balance);

                    var transfers = connector.DataSource<MoneyTransfer>();

                    var transfer = transfers.Single();
                    ClassicAssert.AreEqual(334, transfer.Amount);

                    ClassicAssert.AreEqual(334, transfer.Amount);
                }
            }


            // check that everything is persisted ok
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Account>();
                connector.DeclareCollection<MoneyTransfer>();

                var accounts = connector.DataSource<Account>();
                var src = accounts[accountIds[0]];
                var dst = accounts[accountIds[1]];

                ClassicAssert.AreEqual(666, src.Balance);
                ClassicAssert.AreEqual(334, dst.Balance);

                var transfers = connector.DataSource<MoneyTransfer>();

                var transfer = transfers.Single();

                ClassicAssert.AreEqual(334, transfer.Amount);
            }

            TraceEnd();
        }


        [Test]
        public void Delete_in_transaction()
        {
            TraceBegin();

            int[] accountIds = null;

            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Account>();
                connector.DeclareCollection<MoneyTransfer>();

                var accounts = connector.DataSource<Account>();

                accountIds = connector.GenerateUniqueIds("account_id", 2);

                var tIds = connector.GenerateUniqueIds("transfer_ids", 2);

                accounts.Put(new Account { Id = accountIds[0], Balance = 1000 });
                accounts.Put(new Account { Id = accountIds[1], Balance = 0 });


                // make a many transfers between two accounts

                {
                    var transferredMoney = 334;

                    var transaction = connector.BeginTransaction();
                    transaction.UpdateIf(new Account { Id = accountIds[0], Balance = 1000 - transferredMoney },
                        account => account.Balance >= transferredMoney);

                    transaction.Put(new Account { Id = accountIds[1], Balance = transferredMoney });
                    transaction.Put(new MoneyTransfer
                    {
                        Id = tIds[0],
                        Amount = transferredMoney,
                        SourceAccount = accountIds[0],
                        DestinationAccount = accountIds[1]
                    });
                    transaction.Commit();


                    // check that everything is updated in memory
                    var src = accounts[accountIds[0]];
                    var dst = accounts[accountIds[1]];

                    ClassicAssert.AreEqual(666, src.Balance);
                    ClassicAssert.AreEqual(334, dst.Balance);

                    var transfers = connector.DataSource<MoneyTransfer>();

                    var transfer = transfers.Single();
                    ClassicAssert.AreEqual(334, transfer.Amount);

                    ClassicAssert.AreEqual(334, transfer.Amount);
                }

                // then cancel the transfer

                {
                    var transfers = connector.DataSource<MoneyTransfer>();
                    var transfer = transfers.Single();

                    var transaction = connector.BeginTransaction();

                    transaction.Put(new Account { Id = accountIds[0], Balance = 1000 });
                    transaction.Put(new Account { Id = accountIds[1], Balance = 0 });
                    transaction.Delete(transfer);

                    transaction.Commit();


                    // check that everything is updated in memory
                    var src = accounts[accountIds[0]];
                    var dst = accounts[accountIds[1]];

                    ClassicAssert.AreEqual(1000, src.Balance);
                    ClassicAssert.AreEqual(0, dst.Balance);

                    // no more transfer
                    var transferCount = transfers.Count();
                    ClassicAssert.AreEqual(0, transferCount);
                }
            }


            // check that everything is persisted ok
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Account>();
                connector.DeclareCollection<MoneyTransfer>();

                var accounts = connector.DataSource<Account>();
                var src = accounts[accountIds[0]];
                var dst = accounts[accountIds[1]];

                ClassicAssert.AreEqual(1000, src.Balance);
                ClassicAssert.AreEqual(0, dst.Balance);

                var transfers = connector.DataSource<MoneyTransfer>();


                ClassicAssert.AreEqual(0, transfers.Count());
            }

            TraceEnd();
        }

        [Test]
        public void Delete_many_in_transaction()
        {
            TraceBegin();
            using var connector = new Connector(_clientConfig);

            connector.DeclareCollection<Account>("delete_test");

            const int count = 2000;
            const int classes = 10;

            var accountIds = connector.GenerateUniqueIds("account_id", count);

            var accounts = connector.DataSource<Account>("delete_test");

            var all = new List<Account>(count);

            for (var i = 0; i < count; i++)
            {
                var acc = new Account { Id = accountIds[i], Balance = i % classes };
                all.Add(acc);
            }

            accounts.PutMany(all);

            ClassicAssert.AreEqual(count, accounts.Count());

            ClassicAssert.AreEqual(count / classes, accounts.Count(acc => acc.Balance == 3));

            var random = new Random(Environment.TickCount);

            Parallel.Invoke(
                () =>
                {
                    for (var i = 0; i < classes; i++)
                    {
                        var transaction = connector.BeginTransaction();
                        transaction.DeleteMany<Account>(acc => acc.Balance == i, "delete_test");
                        transaction.Commit();
                        Thread.Sleep(random.Next(200));
                    }
                },
                () =>
                {
                    var notDeleted = count;

                    while (notDeleted > 0)
                    {
                        connector.ConsistentRead(ctx =>
                        {
                            notDeleted = accounts.Count();

                            var chunkSize = count / classes;


                            ClassicAssert.AreEqual(0, notDeleted % chunkSize, "only complete chunks are deleted");

                            Console.WriteLine($"found {notDeleted} items");
                        }, "delete_test");

                        Thread.Sleep(random.Next(200));
                    }
                });
            TraceEnd();
        }

        [Test]
        public void No_deadlock_if_same_objects_are_updated_in_parallel_by_multiple_clients()
        {

            TraceBegin();

            List<Account> myAccounts;
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Account>();
                connector.DeclareCollection<MoneyTransfer>();

                var accounts = connector.DataSource<Account>();

                var accountIds = connector.GenerateUniqueIds("account_id", 2);

                accounts.Put(new Account { Id = accountIds[0], Balance = 1000 });
                accounts.Put(new Account { Id = accountIds[1], Balance = 0 });

                myAccounts = accounts.ToList();
            }

            Parallel.Invoke(
                () =>
                {
                    using var connector1 = new Connector(_clientConfig);
                    connector1.DeclareCollection<Account>();
                    connector1.DeclareCollection<MoneyTransfer>();
                    for (var i = 0; i < 100; i++)
                    {
                        var transfer = new MoneyTransfer
                        {
                            Amount = 10,
                            Date = DateTime.Today,
                            SourceAccount = myAccounts[0].Id,
                            DestinationAccount = myAccounts[1].Id
                        };

                        myAccounts[0].Balance -= 10;
                        myAccounts[1].Balance += 10;

                        var transaction = connector1.BeginTransaction();
                        transaction.Put(myAccounts[0]);
                        transaction.Put(myAccounts[1]);
                        transaction.Put(transfer);
                        transaction.Commit();
                    }
                },
                () =>
                {
                    using var connector2 = new Connector(_clientConfig);
                    connector2.DeclareCollection<Account>();
                    connector2.DeclareCollection<MoneyTransfer>();
                    for (var i = 0; i < 100; i++)
                    {
                        var transfer = new MoneyTransfer
                        {
                            Amount = 10,
                            Date = DateTime.Today,
                            SourceAccount = myAccounts[0].Id,
                            DestinationAccount = myAccounts[1].Id
                        };

                        myAccounts[0].Balance -= 10;
                        myAccounts[1].Balance += 10;

                        var transaction = connector2.BeginTransaction();
                        transaction.Put(myAccounts[0]);
                        transaction.Put(myAccounts[1]);
                        transaction.Put(transfer);
                        transaction.Commit();
                    }
                },
                () =>
                {
                    using var connector3 = new Connector(_clientConfig);
                    connector3.DeclareCollection<Account>();
                    connector3.DeclareCollection<MoneyTransfer>();
                    for (var i = 0; i < 100; i++)
                    {
                        var transfer = new MoneyTransfer
                        {
                            Amount = 10,
                            Date = DateTime.Today,
                            SourceAccount = myAccounts[0].Id,
                            DestinationAccount = myAccounts[1].Id
                        };

                        myAccounts[0].Balance -= 10;
                        myAccounts[1].Balance += 10;

                        var transaction = connector3.BeginTransaction();
                        transaction.Put(myAccounts[0]);
                        transaction.Put(myAccounts[1]);
                        transaction.Put(transfer);
                        transaction.Commit();
                    }
                });

            TransactionStatistics.Display();


            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Account>();
                var accounts = connector.DataSource<Account>();


                myAccounts = accounts.ToList();

                Console.WriteLine($"balance1={myAccounts[0].Balance} balance2={myAccounts[1].Balance}");
            }

            TraceEnd();
        }


        [Test]
        public void No_deadlock_if_same_objects_are_updated_in_parallel_by_one_client()
        {
            TraceBegin();
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Account>();
                connector.DeclareCollection<MoneyTransfer>();
                var accounts = connector.DataSource<Account>();

                var accountIds = connector.GenerateUniqueIds("account_id", 2);

                accounts.Put(new Account { Id = accountIds[0], Balance = 1000 });
                accounts.Put(new Account { Id = accountIds[1], Balance = 0 });

                var myAccounts = accounts.ToList();

                try
                {
                    Parallel.For(0, 100, i =>
                    {
                        var transfer = new MoneyTransfer
                        {
                            Amount = 10,
                            Date = DateTime.Today,
                            SourceAccount = myAccounts[0].Id,
                            DestinationAccount = myAccounts[1].Id
                        };

                        myAccounts[0].Balance -= 10;
                        myAccounts[1].Balance += 10;

                        var transaction = connector.BeginTransaction();
                        transaction.Put(myAccounts[0]);
                        transaction.Put(myAccounts[1]);
                        transaction.Put(transfer);
                        transaction.Commit();
                    });
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    // a transaction can fail. We are testing that there is no deadlock
                }
            }

            TraceEnd();
        }

        [Test]
        public void No_deadlock_if_transactions_and_non_transactional_queries_are_run_in_parallel()
        {
            TraceBegin();

            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Account>();
                connector.DeclareCollection<MoneyTransfer>();

                var accounts = connector.DataSource<Account>();

                var accountIds = connector.GenerateUniqueIds("account_id", 2);

                accounts.Put(new Account { Id = accountIds[0], Balance = 1000 });
                accounts.Put(new Account { Id = accountIds[1], Balance = 0 });


                // run in parallel a sequence of transactions and non transactional read requests 

                try
                {
                    Parallel.Invoke(
                        () =>
                        {
                            Parallel.For(0, Threads, i =>
                            {
                                // this is a non transactional request
                                var myAccounts = accounts.ToList();
                                ClassicAssert.AreEqual(2, myAccounts.Count);

                                var transfers = connector.DataSource<MoneyTransfer>();

                                // this is also a non transactional request
                                var unused = transfers.Where(t => t.SourceAccount == myAccounts[0].Id).ToList();
                            });
                        },
                        () =>
                        {
                            var myAccounts = accounts.ToList();


                            for (var i = 0; i < Threads; i++)
                            {
                                var transfer = new MoneyTransfer
                                {
                                    Amount = 10,
                                    Date = DateTime.Today,
                                    SourceAccount = myAccounts[0].Id,
                                    DestinationAccount = myAccounts[1].Id
                                };

                                myAccounts[0].Balance -= 10;
                                myAccounts[1].Balance += 10;

                                var transaction = connector.BeginTransaction();
                                transaction.Put(myAccounts[0]);
                                transaction.Put(myAccounts[1]);
                                transaction.Put(transfer);
                                transaction.Commit();
                            }
                        });
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Assert.Fail(e.Message);
                }
            }

            // check that the data is persistent (force the external server to reload data)
            StopServers();
            StartServers();
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Account>();
                connector.DeclareCollection<MoneyTransfer>();

                var accounts = connector.DataSource<Account>();
                var myAccounts = accounts.ToList();
                ClassicAssert.AreEqual(2, myAccounts.Count);


                var sum = myAccounts.Sum(acc => acc.Balance);
                ClassicAssert.AreEqual(1000, sum);

                ClassicAssert.IsTrue(myAccounts.All(acc => acc.Balance != 1000),
                    "The balance is unchanged when reloading data");

                Console.WriteLine($"balance1={myAccounts[0].Balance} balance2={myAccounts[1].Balance}");
            }

            TraceEnd();
        }


        [Test]
        public void Consistent_reads_in_parallel()
        {
            TraceBegin();

            using var connector = new Connector(_clientConfig);

            connector.DeclareCollection<Account>();
            connector.DeclareCollection<MoneyTransfer>();

            var accounts = connector.DataSource<Account>();

            var accountIds = connector.GenerateUniqueIds("account_id", 2);

            accounts.Put(new Account { Id = accountIds[0], Balance = 1000 });
            accounts.Put(new Account { Id = accountIds[1], Balance = 0 });

            Parallel.For(0, 20, i =>
            {
                // ReSharper disable once AccessToDisposedClosure
                connector.ConsistentRead<MoneyTransfer, Account>(ctx =>
                {
                    var myAccounts = ctx.Collection<Account>().ToList();
                    ClassicAssert.AreEqual(2, myAccounts.Count);

                    // with consistent reed we do not see the updates during a transaction. The sum of the accounts balance should always be 1000
                    ClassicAssert.AreEqual(1000, myAccounts.Sum(acc => acc.Balance));

                    var transfers = ctx.Collection<MoneyTransfer>();


                    var unused = transfers.Where(t => t.SourceAccount == myAccounts[0].Id).ToList();
                });
            });

            TraceEnd();
        }

        [Test]
        public void Sequential_consistent_reads_and_transactions()
        {
            TraceBegin();

            using var connector = new Connector(_clientConfig);

            connector.DeclareCollection<Account>();
            connector.DeclareCollection<MoneyTransfer>();

            var accounts = connector.DataSource<Account>();

            var accountIds = connector.GenerateUniqueIds("account_id", 2);

            accounts.Put(new Account { Id = accountIds[0], Balance = 1000 });
            accounts.Put(new Account { Id = accountIds[1], Balance = 0 });


            // ReSharper disable once AccessToDisposedClosure
            connector.ConsistentRead<MoneyTransfer, Account>(ctx =>
            {
                var myAccounts = ctx.Collection<Account>().ToList();
                ClassicAssert.AreEqual(2, myAccounts.Count);

                // with consistent reed we do not see the updates during a transaction. The sum of the accounts balance should always be 1000
                ClassicAssert.AreEqual(1000, myAccounts.Sum(acc => acc.Balance));

                var transfers = ctx.Collection<MoneyTransfer>();


                var unused = transfers.Where(t => t.SourceAccount == myAccounts[0].Id).ToList();
            });


            var all = accounts.ToList();

            var transfer = new MoneyTransfer
            {
                Amount = 10,
                Date = DateTime.Today,
                SourceAccount = all[0].Id,
                DestinationAccount = all[1].Id
            };

            all[0].Balance -= 10;
            all[1].Balance += 10;

            var transaction = connector.BeginTransaction();
            transaction.Put(all[0]);
            transaction.Put(all[1]);
            transaction.Put(transfer);
            transaction.Commit();

            TraceEnd();
        }


        [Test]
        public void Move_data_from_one_collection_to_another_with_transactions()
        {
            TraceBegin();

            using var connector = new Connector(_clientConfig);

            connector.DeclareCollection<Order>("new_orders");
            connector.DeclareCollection<Order>("processed_orders");

            const int count = 100;

            var all = new List<Order>();
            for (var i = 0; i < count; i++) all.Add(new Order { Id = Guid.NewGuid(), Amount = 50, Category = "geek" });

            var newOrders = connector.DataSource<Order>("new_orders");
            connector.DataSource<Order>("processed_orders");

            newOrders.PutMany(all);

            // using transactions move orders from one collection to another
            // and consistent read to check that at any time data is consistent

            Parallel.Invoke(() =>
                {
                    foreach (var order in newOrders.ToList())
                    {
                        var transaction = connector.BeginTransaction();
                        order.IsDelivered = true;
                        transaction.Put(order, "processed_orders");
                        transaction.Delete(order, "new_orders");
                        transaction.Commit();

                        Thread.SpinWait(10000);
                    }
                },
                () =>
                {
                    for (var i = 0; i < count; i++)
                        connector.ConsistentRead(ctx =>
                        {
                            var @new = ctx.Collection<Order>("new_orders").ToList();
                            var processed = ctx.Collection<Order>("processed_orders").ToList();

                            ClassicAssert.AreEqual(count, processed.Count + @new.Count);
                            ClassicAssert.IsTrue(processed.All(o => o.IsDelivered = true));

                            Console.WriteLine($"{@new.Count} - {processed.Count}");
                        }, "new_orders", "processed_orders");
                });

            TraceEnd();
        }

        [Test]
        public void Consistent_reads_and_transactions_run_in_parallel()
        {
            TraceBegin();

            using var connector = new Connector(_clientConfig);

            connector.DeclareCollection<Account>();
            connector.DeclareCollection<MoneyTransfer>();

            var accounts = connector.DataSource<Account>();


            var accountIds = connector.GenerateUniqueIds("account_id", 2);
            var transferIds = connector.GenerateUniqueIds("transfer_id", Threads);

            accounts.Put(new Account { Id = accountIds[0], Balance = 1000 });
            accounts.Put(new Account { Id = accountIds[1], Balance = 0 });


            var watch = new Stopwatch();
            watch.Start();


            //run in parallel a sequence of transactions and consistent read-only operation
            var all = accounts.ToList();
            try
            {
                Parallel.Invoke(
                    () =>
                    {
                        Parallel.For(0, Threads, new ParallelOptions { MaxDegreeOfParallelism = 10 }, i =>
                        {
                            // ReSharper disable once AccessToDisposedClosure
                            connector.ConsistentRead(ctx =>
                            {
                                var myAccounts = ctx.Collection<Account>().ToList();

                                Dbg.Trace($"Done step 1 for iteration {i}");

                                ClassicAssert.AreEqual(2, myAccounts.Count);

                                // with consistent reed we do not see the updates during a transaction. The sum of the accounts balance should always be 1000
                                ClassicAssert.AreEqual(1000, myAccounts.Sum(acc => acc.Balance));

                                var transfers = ctx.Collection<MoneyTransfer>();


                                var tr = transfers.Where(t => t.SourceAccount == myAccounts[0].Id).ToList();

                                Dbg.Trace($"Done step 2 for iteration {i}");

                                //check consistency between transfer and balance

                                var sumTransferred = tr.Sum(tr => tr.Amount);

                                ClassicAssert.AreEqual(sumTransferred, myAccounts[1].Balance);

                                Console.WriteLine($"Balance = {myAccounts[1].Balance} Transferred = {sumTransferred}");

                                Dbg.Trace($"Done for iteration {i}");
                            }, nameof(MoneyTransfer), nameof(Account));
                        });
                    },
                    () =>
                    {
                        for (var i = 0; i < Threads; i++)
                        {
                            var transfer = new MoneyTransfer
                            {
                                Id = transferIds[i],
                                Amount = 10,
                                Date = DateTime.Today,
                                SourceAccount = all[0].Id,
                                DestinationAccount = all[1].Id
                            };

                            all[0].Balance -= 10;
                            all[1].Balance += 10;

                            var transaction = connector.BeginTransaction();
                            transaction.Put(all[0]);
                            transaction.Put(all[1]);
                            transaction.Put(transfer);
                            transaction.Commit();

                            Console.WriteLine("Transaction committed");
                        }
                    });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Assert.Fail(e.Message);
            }

            watch.Stop();

            Console.WriteLine($"{Threads} threads on {Servers} servers took {watch.ElapsedMilliseconds} ms");

            // check that the data is persistent (force the external server to reload data)
            //StopServers();
            //StartServers();
            //using (var connector = new Connector(_clientConfig))
            //{
            //    connector.DeclareCollection<Account>();  
            //    connector.DeclareCollection<MoneyTransfer>();  

            //    var accounts = connector.DataSource<Account>();
            //    var myAccounts = accounts.ToList();
            //    ClassicAssert.AreEqual(2, myAccounts.Count);


            //    var sum = myAccounts.Sum(acc => acc.Balance);
            //    ClassicAssert.AreEqual(1000, sum);

            //    ClassicAssert.IsTrue(myAccounts.All(acc => acc.Balance != 1000),
            //        "The balance is unchanged when reloading data");

            //    Console.WriteLine($"balance1={myAccounts[0].Balance} balance2={myAccounts[1].Balance}");
            //}

            TraceEnd();
        }


        [Test]
        public void Simple_transaction()
        {
            TraceBegin();

            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Account>();
                connector.DeclareCollection<MoneyTransfer>();

                var accounts = connector.DataSource<Account>();

                var accountIds = connector.GenerateUniqueIds("account_id", 2);

                var srcAccount = new Account { Id = accountIds[0], Balance = 1000 };
                var dstAccount = new Account { Id = accountIds[1], Balance = 0 };

                accounts.Put(srcAccount);
                accounts.Put(dstAccount);


                // check the sum of two balances is always 1000
                var myAccounts = accounts.ToList();
                ClassicAssert.AreEqual(2, myAccounts.Count);

                var sum = myAccounts.Sum(acc => acc.Balance);
                ClassicAssert.AreEqual(1000, sum);

                srcAccount.Balance -= 10;
                dstAccount.Balance += 10;


                var transfer = new MoneyTransfer
                {
                    Amount = 10,
                    Date = DateTime.Today,
                    SourceAccount = myAccounts[0].Id,
                    DestinationAccount = myAccounts[1].Id
                };

                var transaction = connector.BeginTransaction();
                transaction.Put(srcAccount);
                transaction.Put(dstAccount);
                transaction.Put(transfer);
                transaction.Commit();

                // check the some of two balances is always 1000
                myAccounts = accounts.ToList();
                ClassicAssert.AreEqual(2, myAccounts.Count);

                sum = myAccounts.Sum(acc => acc.Balance);
                ClassicAssert.AreEqual(1000, sum);

                ClassicAssert.IsFalse(myAccounts.Any(acc => acc.Balance == 0));

                Console.WriteLine($"balance1={myAccounts[0].Balance} balance2={myAccounts[1].Balance}");
            }

            TraceEnd();
        }
    }
}