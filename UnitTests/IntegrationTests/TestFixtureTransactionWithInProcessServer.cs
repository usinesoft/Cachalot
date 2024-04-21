using System;
using System.IO;
using System.Linq;
using Cachalot.Linq;
using Client.Interface;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Server.Persistence;
using Tests.TestData.MoneyTransfer;

namespace Tests.IntegrationTests
{
    [TestFixture]
    public class TestFixtureTransactionWithInProcessServer
    {
        [SetUp]
        public void SetUp()
        {
            if (Directory.Exists(Constants.DataPath)) Directory.Delete(Constants.DataPath, true);
        }

        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            Environment.CurrentDirectory = TestContext.CurrentContext.TestDirectory;
            Directory.SetCurrentDirectory(TestContext.CurrentContext.TestDirectory);
        }


        [Test]
        public void Conditional_update_in_transaction()
        {
            int[] accountIds = null;

            using (var connector = new Connector(new ClientConfig()))
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
            using (var connector = new Connector(new ClientConfig()))
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
        }
    }
}