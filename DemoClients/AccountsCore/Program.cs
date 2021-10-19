using System;
using System.Diagnostics;
using System.Linq;
using Cachalot.Linq;
using Client.Core;
using Client.Interface;

namespace Accounts
{
    internal partial class Program
    {

        const int TestIterations = 1000;
        
        private static void Main(string[] args)
        {
            
            
            try
            {
                // quick test with a cluster of two nodes
                using var connector = new Connector("localhost:48401+localhost:48402");
                
                Title("test with a cluster of two servers");

                    
                PerfTest(connector);
            }
            catch (CacheException e)
            {
                Console.WriteLine(e.Message);
            }

            try
            {
                // quick test with one external server
                using var connector = new Connector("localhost:48401" );

                Title("test with one server");
                
                PerfTest(connector);
            }
            catch (CacheException e)
            {
                Console.WriteLine(e.Message);
            }

            try
            {
                // quick test with in-process server
                using var connector = new Connector(new ClientConfig { IsPersistent = true });

                Title("test with in-process server");
                
                PerfTest(connector);
            }
            catch (CacheException e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private static void PerfTest(Connector connector)
        {
            
            TransactionStatistics.Reset();
            // first delete all data to start with a clean database
            connector.AdminInterface().DropDatabase();
            
            connector.DeclareCollection<Account>();
            connector.DeclareCollection<MoneyTransfer>();

            var accounts = connector.DataSource<Account>();

            Header($"creating {TestIterations} accounts");
            
            RunOnce(() =>
            {
                for (int i = 0; i < TestIterations; i++)
                {
                    accounts.Put(new Account { Id = i, Balance = 1000 });
                }
            }, "account creation");
            


            var ids = Enumerable.Range(0, TestIterations).ToArray();

            try
            {
                ids = connector.GenerateUniqueIds("transfer_id", TestIterations);
            }
            catch (Exception)
            {
                // ignore (means testing with non persistent server)
            }
            

            var rand = new Random();

            int failed = 0;

            Header($"creating data transfers between random accounts and update the source and destination account transactionally");
            var sw = new Stopwatch();

            sw.Start();

            for (int i = 0; i < TestIterations; i++)
            {
                var src = rand.Next(1, 1000);
                var dst = src + 1 > 999? src-1:src+1;

                var transferred = rand.Next(1, 1000);

                var srcAccount = accounts[src];
                var dstAccount = accounts[dst];

                srcAccount.Balance -= transferred;
                dstAccount.Balance += transferred;

                var transaction = connector.BeginTransaction();
                transaction.UpdateIf(srcAccount, account => account.Balance >= transferred);
                transaction.Put(dstAccount);
                transaction.Put(new MoneyTransfer
                {
                    Amount = transferred,
                    Date = DateTime.Today,
                    SourceAccount = src,
                    DestinationAccount = dst,
                    Id = ids[i]
                });

                try
                {
                    transaction.Commit();
                }
                catch (CacheException e)
                {
                    if (e.IsTransactionException)
                    {
                        failed++;
                    }
                }


            }

            sw.Stop();

            Console.WriteLine($"{TestIterations} transactions took {sw.ElapsedMilliseconds} milliseconds. Rolled back transactions: {failed}. ");
            var stats = TransactionStatistics.AsString();

            Console.WriteLine(stats);

            // check that all accounts have positive balance
            var negatives = accounts.Count(acc => acc.Balance < 0);

            if (negatives > 0)
            {
                Console.WriteLine("Some accounts have negative balance");
            }
            else
            {
                Console.WriteLine("All accounts have positive balance");
            }


        }

    }
}