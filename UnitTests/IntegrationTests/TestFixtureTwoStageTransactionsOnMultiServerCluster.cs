using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cachalot.Linq;
using Channel;
using Client;
using Client.Core;
using Client.Interface;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Interfaces;
using NUnit.Framework;
using Server;
using Tests.TestData.MoneyTransfer;

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
            for (var i = 0; i < ServerCount; i++)
                if (Directory.Exists($"server{i:D2}"))
                    Directory.Delete($"server{i:D2}", true);


            StartServers();
        }

        private class ServerInfo
        {
            public TcpServerChannel Channel { get; set; }
            public Server.Server Server { get; set; }
            public int Port { get; set; }
        }

        private List<ServerInfo> _servers = new List<ServerInfo>();

        private const int ServerCount = 3;

        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            Environment.CurrentDirectory = TestContext.CurrentContext.TestDirectory;
            Directory.SetCurrentDirectory(TestContext.CurrentContext.TestDirectory);
        }

        private void StopServers()
        {
            foreach (var serverInfo in _servers)
            {
                serverInfo.Channel.Stop();
                serverInfo.Server.Stop();
            }
        }


        private void RestartOneServer()
        {
            var serverInfo = _servers[0];

            serverInfo.Channel.Stop();
            serverInfo.Server.Stop();

            // restart on the same port
            serverInfo.Port = serverInfo.Channel.Init(serverInfo.Port);
            serverInfo.Channel.Start();
            serverInfo.Server.Start();

            Thread.Sleep(500);
        }


        private ClientConfig _clientConfig;

        private void StartServers(int serverCount = 0)
        {
            _clientConfig = new ClientConfig();
            _servers = new List<ServerInfo>();

            serverCount = serverCount == 0 ? ServerCount : serverCount;

            for (var i = 0; i < serverCount; i++)
            {
                var serverInfo = new ServerInfo {Channel = new TcpServerChannel()};
                var nodeConfig = new NodeConfig {IsPersistent = true, DataPath = $"server{i:D2}"};

                serverInfo.Server =
                    new Server.Server(nodeConfig) {Channel = serverInfo.Channel};

                serverInfo.Port = serverInfo.Channel.Init();
                serverInfo.Channel.Start();
                serverInfo.Server.Start();

                _servers.Add(serverInfo);

                _clientConfig.Servers.Add(
                    new ServerConfig {Host = "localhost", Port = serverInfo.Port});
            }

            _clientConfig.ConnectionPoolCapacity = 10;
            _clientConfig.PreloadedConnections = 4;


            Thread.Sleep(500); //be sure the server nodes are started
        }

        [Test]
        public void Conditional_update_in_transaction()
        {
            int[] accountIds = null;

            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Account>();  
                connector.DeclareCollection<MoneyTransfer>();  

                var accounts = connector.DataSource<Account>();

                accountIds = connector.GenerateUniqueIds("account_id", 2);

                var tIds = connector.GenerateUniqueIds("transfer_ids", 2);

                accounts.Put(new Account {Id = accountIds[0], Balance = 1000});
                accounts.Put(new Account {Id = accountIds[1], Balance = 0});


                // first transaction should succeed

                {
                    var transferredMoney = 334;

                    var transaction = connector.BeginTransaction();
                    transaction.UpdateIf(new Account {Id = accountIds[0], Balance = 1000 - transferredMoney},
                        account => account.Balance >= transferredMoney);

                    transaction.Put(new Account {Id = accountIds[1], Balance = transferredMoney});
                    transaction.Put(new MoneyTransfer
                    {
                        Id = tIds[0], Amount = transferredMoney, SourceAccount = accountIds[0],
                        DestinationAccount = accountIds[1]
                    });
                    transaction.Commit();


                    // check that everything is updated in memory
                    var src = accounts[accountIds[0]];
                    var dst = accounts[accountIds[1]];

                    Assert.AreEqual(666, src.Balance);
                    Assert.AreEqual(334, dst.Balance);

                    var transfers = connector.DataSource<MoneyTransfer>();

                    var transfer = transfers.Single();
                    Assert.AreEqual(334, transfer.Amount);

                    Assert.AreEqual(334, transfer.Amount);
                }


                // second transaction should fail
                {
                    var transferredMoney = 1001;

                    var transaction = connector.BeginTransaction();
                    transaction.UpdateIf(new Account {Id = accountIds[0], Balance = 1000 - transferredMoney},
                        account => account.Balance >= transferredMoney);

                    transaction.Put(new Account {Id = accountIds[1], Balance = transferredMoney});
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
                        Assert.IsTrue(e.IsTransactionException);
                        Assert.AreEqual(ExceptionType.ConditionNotSatisfied, e.ExceptionType);

                        Console.WriteLine(e);
                    }


                    // check that nothing is updated in memory
                    var src = accounts[accountIds[0]];
                    var dst = accounts[accountIds[1]];

                    Assert.AreEqual(666, src.Balance);
                    Assert.AreEqual(334, dst.Balance);

                    var transfers = connector.DataSource<MoneyTransfer>();

                    var transfer = transfers.Single();
                    Assert.AreEqual(334, transfer.Amount);

                    Assert.AreEqual(334, transfer.Amount);
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

                Assert.AreEqual(666, src.Balance);
                Assert.AreEqual(334, dst.Balance);

                var transfers = connector.DataSource<MoneyTransfer>();

                var transfer = transfers.Single();

                Assert.AreEqual(334, transfer.Amount);
            }
        }


        [Test]
        public void Delete_in_transaction()
        {
            int[] accountIds = null;

            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Account>();  
                connector.DeclareCollection<MoneyTransfer>();  

                var accounts = connector.DataSource<Account>();

                accountIds = connector.GenerateUniqueIds("account_id", 2);

                var tIds = connector.GenerateUniqueIds("transfer_ids", 2);

                accounts.Put(new Account {Id = accountIds[0], Balance = 1000});
                accounts.Put(new Account {Id = accountIds[1], Balance = 0});


                // make a many transfer between two accounts

                {
                    var transferredMoney = 334;

                    var transaction = connector.BeginTransaction();
                    transaction.UpdateIf(new Account {Id = accountIds[0], Balance = 1000 - transferredMoney},
                        account => account.Balance >= transferredMoney);

                    transaction.Put(new Account {Id = accountIds[1], Balance = transferredMoney});
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

                    Assert.AreEqual(666, src.Balance);
                    Assert.AreEqual(334, dst.Balance);

                    var transfers = connector.DataSource<MoneyTransfer>();

                    var transfer = transfers.Single();
                    Assert.AreEqual(334, transfer.Amount);

                    Assert.AreEqual(334, transfer.Amount);
                }

                // then cancel the transfer

                {
                    var transfers = connector.DataSource<MoneyTransfer>();
                    var transfer = transfers.Single();

                    var transaction = connector.BeginTransaction();

                    transaction.Put(new Account {Id = accountIds[0], Balance = 1000});
                    transaction.Put(new Account {Id = accountIds[1], Balance = 0});
                    transaction.Delete(transfer);

                    transaction.Commit();


                    // check that everything is updated in memory
                    var src = accounts[accountIds[0]];
                    var dst = accounts[accountIds[1]];

                    Assert.AreEqual(1000, src.Balance);
                    Assert.AreEqual(0, dst.Balance);

                    // no more transfer
                    var transferCount = transfers.Count();
                    Assert.AreEqual(0, transferCount);
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

                Assert.AreEqual(1000, src.Balance);
                Assert.AreEqual(0, dst.Balance);

                var transfers = connector.DataSource<MoneyTransfer>();


                Assert.AreEqual(0, transfers.Count());
            }
        }

        [Test]
        public void No_deadlock_if_same_objects_are_updated_in_parallel_by_multiple_clients()
        {
            List<Account> myAccounts;
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Account>();
                connector.DeclareCollection<MoneyTransfer>();

                var accounts = connector.DataSource<Account>();

                var accountIds = connector.GenerateUniqueIds("account_id", 2);

                accounts.Put(new Account {Id = accountIds[0], Balance = 1000});
                accounts.Put(new Account {Id = accountIds[1], Balance = 0});

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
        }


        [Test]
        public void No_deadlock_if_same_objects_are_updated_in_parallel_by_one_client()
        {
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Account>();
                var accounts = connector.DataSource<Account>();

                var accountIds = connector.GenerateUniqueIds("account_id", 2);

                accounts.Put(new Account {Id = accountIds[0], Balance = 1000});
                accounts.Put(new Account {Id = accountIds[1], Balance = 0});

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
        }

        [Test]
        public void No_deadlock_if_transactions_and_non_transactional_queries_are_run_in_parallel()
        {
            using (var connector = new Connector(_clientConfig))
            {

                connector.DeclareCollection<Account>();  
                connector.DeclareCollection<MoneyTransfer>();  

                var accounts = connector.DataSource<Account>();

                var accountIds = connector.GenerateUniqueIds("account_id", 2);

                accounts.Put(new Account {Id = accountIds[0], Balance = 1000});
                accounts.Put(new Account {Id = accountIds[1], Balance = 0});


                // run in parallel a sequence of transactions and non transactional read requests 
                
                try
                {
                    Parallel.Invoke(
                        () =>
                        {
                            Parallel.For(0, 200, i =>
                            {
                                // this is a non transactional request
                                var myAccounts = accounts.ToList();
                                Assert.AreEqual(2, myAccounts.Count);

                                var transfers = connector.DataSource<MoneyTransfer>();
                                
                                // this is also a non transactional request
                                var unused = transfers.Where(t => t.SourceAccount == myAccounts[0].Id).ToList();

                            });
                        },
                        () =>
                        {
                            List<Account> myAccounts = accounts.ToList();

                            
                            for (var i = 0; i < 200; i++)
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
                Assert.AreEqual(2, myAccounts.Count);


                var sum = myAccounts.Sum(acc => acc.Balance);
                Assert.AreEqual(1000, sum);

                Assert.IsTrue(myAccounts.All(acc => acc.Balance != 1000),
                    "The balance is unchanged when reloading data");

                Console.WriteLine($"balance1={myAccounts[0].Balance} balance2={myAccounts[1].Balance}");
            }
        }


        [Test]
        public void Consistent_reads_in_parallel()
        {
            using var connector = new Connector(_clientConfig);
            
            connector.DeclareCollection<Account>();  
            connector.DeclareCollection<MoneyTransfer>();  

            var accounts = connector.DataSource<Account>();

            var accountIds = connector.GenerateUniqueIds("account_id", 2);

            accounts.Put(new Account {Id = accountIds[0], Balance = 1000});
            accounts.Put(new Account {Id = accountIds[1], Balance = 0});

            Parallel.For(0, 20, i =>
            {

                // ReSharper disable once AccessToDisposedClosure
                connector.ConsistentRead(ctx =>
                {
                                    
                    var myAccounts = ctx.Collection<Account>().ToList();
                    Assert.AreEqual(2, myAccounts.Count);

                    // with consistent reed we do not see the updates during a transaction. The sum of the accounts balance should always be 1000
                    Assert.AreEqual(1000, myAccounts.Sum(acc=>acc.Balance));

                    var transfers = ctx.Collection<MoneyTransfer>();
                                
                                    
                    var unused = transfers.Where(t => t.SourceAccount == myAccounts[0].Id).ToList();

                }, typeof(MoneyTransfer).FullName, typeof(Account).FullName);


            });

        }

        [Test]
        public void Sequential_consistent_reads_and_transactions()
        {
            using var connector = new Connector(_clientConfig);
            
            connector.DeclareCollection<Account>();  
            connector.DeclareCollection<MoneyTransfer>();  

            var accounts = connector.DataSource<Account>();

            var accountIds = connector.GenerateUniqueIds("account_id", 2);

            accounts.Put(new Account {Id = accountIds[0], Balance = 1000});
            accounts.Put(new Account {Id = accountIds[1], Balance = 0});

           
            // ReSharper disable once AccessToDisposedClosure
            connector.ConsistentRead(ctx =>
            {
                                
                var myAccounts = ctx.Collection<Account>().ToList();
                Assert.AreEqual(2, myAccounts.Count);

                // with consistent reed we do not see the updates during a transaction. The sum of the accounts balance should always be 1000
                Assert.AreEqual(1000, myAccounts.Sum(acc=>acc.Balance));

                var transfers = ctx.Collection<MoneyTransfer>();
                            
                                
                var unused = transfers.Where(t => t.SourceAccount == myAccounts[0].Id).ToList();

            }, typeof(MoneyTransfer).FullName, typeof(Account).FullName);


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

           

        }

        [Test]
        public void Consistent_reads_and_transactions_run_in_parallel()
        {
            using (var connector = new Connector(_clientConfig))
            {

                connector.DeclareCollection<Account>();  
                connector.DeclareCollection<MoneyTransfer>();  

                var accounts = connector.DataSource<Account>();

                const int iterations = 101;

                var accountIds = connector.GenerateUniqueIds("account_id", 2);
                var transferIds = connector.GenerateUniqueIds("transfer_id", iterations);

                accounts.Put(new Account {Id = accountIds[0], Balance = 1000});
                accounts.Put(new Account {Id = accountIds[1], Balance = 0});


                var watch = new Stopwatch();
                watch.Start();

                // run in parallel a sequence of transactions and consistent read-only operation 
                List<Account> all = accounts.ToList();
                try
                {
                    Parallel.Invoke(
                        () =>
                        {
                            Parallel.For(0, iterations, i =>
                            {

                                // ReSharper disable once AccessToDisposedClosure
                                connector.ConsistentRead(ctx =>
                                {
                                    
                                    var myAccounts = ctx.Collection<Account>().ToList();
                                    Assert.AreEqual(2, myAccounts.Count);

                                    // with consistent reed we do not see the updates during a transaction. The sum of the accounts balance should always be 1000
                                    Assert.AreEqual(1000, myAccounts.Sum(acc=>acc.Balance));

                                    var transfers = ctx.Collection<MoneyTransfer>();
                                
                                    
                                    var tr = transfers.Where(t => t.SourceAccount == myAccounts[0].Id).ToList();

                                    var trAll = transfers.ToList();

                                    //check consistency between transfer and balance

                                    var sumTransferred = tr.Sum(tr => tr.Amount);

                                    Assert.AreEqual(sumTransferred,myAccounts[1].Balance);


                                }, typeof(MoneyTransfer).FullName, typeof(Account).FullName);

                                

                            });
                        },
                        () =>
                        {
                            
                            for (var i = 0; i < iterations; i++)
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
                            }
                        });
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Assert.Fail(e.Message);
                }

                watch.Stop();

                Console.WriteLine($"{iterations} iterations took {watch.ElapsedMilliseconds} ms");
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
                Assert.AreEqual(2, myAccounts.Count);


                var sum = myAccounts.Sum(acc => acc.Balance);
                Assert.AreEqual(1000, sum);

                Assert.IsTrue(myAccounts.All(acc => acc.Balance != 1000),
                    "The balance is unchanged when reloading data");

                Console.WriteLine($"balance1={myAccounts[0].Balance} balance2={myAccounts[1].Balance}");
            }
        }


        [Test]
        public void Simple_transaction()
        {
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<Account>();  
                connector.DeclareCollection<MoneyTransfer>();  

                var accounts = connector.DataSource<Account>();

                var accountIds = connector.GenerateUniqueIds("account_id", 2);

                var srcAccount = new Account {Id = accountIds[0], Balance = 1000};
                var dstAccount = new Account {Id = accountIds[1], Balance = 0};

                accounts.Put(srcAccount);
                accounts.Put(dstAccount);


                // check the sum of two balances is always 1000
                var myAccounts = accounts.ToList();
                Assert.AreEqual(2, myAccounts.Count);

                var sum = myAccounts.Sum(acc => acc.Balance);
                Assert.AreEqual(1000, sum);

                srcAccount.Balance -= 10;
                dstAccount.Balance += 10;


                var transfer = new MoneyTransfer
                {
                    Amount = 10, Date = DateTime.Today, SourceAccount = myAccounts[0].Id,
                    DestinationAccount = myAccounts[1].Id
                };

                var transaction = connector.BeginTransaction();
                transaction.Put(srcAccount);
                transaction.Put(dstAccount);
                transaction.Put(transfer);
                transaction.Commit();

                // check the some of two balances is always 1000
                myAccounts = accounts.ToList();
                Assert.AreEqual(2, myAccounts.Count);

                sum = myAccounts.Sum(acc => acc.Balance);
                Assert.AreEqual(1000, sum);

                Assert.IsFalse(myAccounts.Any(acc => acc.Balance == 0));

                Console.WriteLine($"balance1={myAccounts[0].Balance} balance2={myAccounts[1].Balance}");
            }
        }
    }
}