using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cachalot.Linq;
using Channel;
using Client;
using Client.Core;
using Client.Interface;
using NUnit.Framework;
using Server;
using UnitTests.TestData.MoneyTransfer;

namespace UnitTests
{
    [TestFixture]
    public class TestFixtureTwoStageTransactionsOneServer
    {
        private class ServerInfo
        {
            public TcpServerChannel Channel { get; set; }
            public Server.Server Server { get; set; }
            public int Port { get; set; }
        }

        private List<ServerInfo> _servers = new List<ServerInfo>();

        private const int ServerCount = 1;

        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            Environment.CurrentDirectory = TestContext.CurrentContext.TestDirectory;
            Directory.SetCurrentDirectory(TestContext.CurrentContext.TestDirectory);
        }

        [TearDown]
        public void Exit()
        {
            StopServers();

            // deactivate all failure simulations
            Dbg.DeactivateSimulation();
        }

        private void StopServers()
        {
            foreach (var serverInfo in _servers)
            {
                serverInfo.Channel.Stop();
                serverInfo.Server.Stop();
            }
        }


        private ClientConfig _clientConfig;

        [SetUp]
        public void Init()
        {
            for (var i = 0; i < ServerCount; i++)
                if (Directory.Exists($"server{i:D2}"))
                    Directory.Delete($"server{i:D2}", true);


            StartServers();

            TransactionStatistics.Reset();
        }

        private void StartServers(int serverCount = 0)
        {
            _clientConfig = new ClientConfig();
            _servers = new List<ServerInfo>();

            serverCount = serverCount == 0 ? ServerCount : serverCount;

            for (var i = 0; i < serverCount; i++)
            {

                var serverInfo = new ServerInfo {Channel = new TcpServerChannel()};
                var nodeConfig = new NodeConfig{IsPersistent = true, DataPath = $"server{i:D2}"};
                serverInfo.Server =
                    new Server.Server(nodeConfig) {Channel = serverInfo.Channel};
                serverInfo.Port = serverInfo.Channel.Init();
                serverInfo.Channel.Start();
                serverInfo.Server.Start();

                _servers.Add(serverInfo);

                _clientConfig.Servers.Add(
                    new Client.Interface.ServerConfig {Host = "localhost", Port = serverInfo.Port});
            }


            Thread.Sleep(500); //be sure the server nodes are started
        }


        [Test]
        public void Simple_transaction()
        {
            using (var connector = new Connector(_clientConfig))
            {
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

        [Test]
        public void Money_transfer_in_parallel_transaction()
        {
            using (var connector = new Connector(_clientConfig))
            {
                var accounts = connector.DataSource<Account>();

                var accountIds = connector.GenerateUniqueIds("account_id", 2);

                accounts.Put(new Account {Id = accountIds[0], Balance = 1000});
                accounts.Put(new Account {Id = accountIds[1], Balance = 0});


                // run in parallel a sequence of transactions and clients that check that the sum of the balances of the two accounts is 
                // always the same (thus proving that the two accounts are updated transactionally)

                Parallel.Invoke(
                    () =>
                    {
                        Parallel.For(0, 100, i =>
                        {
                            // check the some of two balances is always 1000
                            var myAccounts = accounts.ToList();
                            Assert.AreEqual(2, myAccounts.Count);

                            var sum = myAccounts.Sum(acc => acc.Balance);
                            Assert.AreEqual(1000, sum);

                            Console.WriteLine($"balance1={myAccounts[0].Balance} balance2={myAccounts[1].Balance}");
                        });
                    },
                    () =>
                    {
                        var myAccounts = accounts.ToList();

                        for (var i = 0; i < 80; i++)
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

            // check that the data is persistent (force the external server to reload data)
            StopServers();
            StartServers();
            using (var connector = new Connector(_clientConfig))
            {
                var accounts = connector.DataSource<Account>();
                var myAccounts = accounts.ToList();
                Assert.AreEqual(2, myAccounts.Count);


                var sum = myAccounts.Sum(acc => acc.Balance);
                Assert.AreEqual(1000, sum);

                Assert.IsTrue(myAccounts.All(acc => acc.Balance < 1000),
                    "The balance is unchanged when reloading data");

                Console.WriteLine($"balance1={myAccounts[0].Balance} balance2={myAccounts[1].Balance}");
            }
        }

        [Test]
        public void Conditional_update_in_transaction()
        {
            int[] accountIds = null;

            using (var connector = new Connector(_clientConfig))
            {
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
    }
}