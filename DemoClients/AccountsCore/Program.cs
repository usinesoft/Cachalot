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

        private static int _testIterations = 1000;
        
        
        private static string _connectionString = "localhost:48401";

        private static void Main(string[] args)
        {
            
            Title("Test application for Cachalot DB");
            
            Console.WriteLine("Command line options (optional):");
            Console.WriteLine();
            Console.WriteLine("1) connection string: host:port or host1:port1+host2;port2... or --internal to run an in-process server");
            Console.WriteLine($"    by default it will try to connect to {_connectionString}");

            Console.WriteLine("2) number of transactions for this test");
            Console.WriteLine($"    by default {_testIterations}");
            
            if (args.Length > 0)
            {
                try
                {
                    _connectionString = args[0];
                    if (_connectionString == "--internal")
                    {
                        _connectionString = null;
                    }

                    if (args.Length > 1)
                    {
                        _testIterations = int.Parse(args[1]);
                    }
                    
                }
                catch (Exception )
                {
                    Console.WriteLine("Invalid command line: using default values");
                }
            }

            try
            {
                // quick test with a cluster of two nodes
                using var connector = new Connector(_connectionString);
                
                Title("Starting test with " + _connectionString);

                    
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
            
            connector.DeclareCollection<Account>();
            connector.DeclareCollection<MoneyTransfer>();

            var accounts = connector.DataSource<Account>();
            var transfers = connector.DataSource<MoneyTransfer>();

            // first delete all data to start with a clean database
            accounts.Truncate();
            transfers.Truncate();

            Header($"creating {_testIterations} accounts");
            
            RunOnce(() =>
            {
                for (int i = 0; i < _testIterations; i++)
                {
                    accounts.Put(new Account { Id = i, Balance = 1000 });
                }
            }, "account creation");
            


            var ids = Enumerable.Range(0, _testIterations).ToArray();

            try
            {
                ids = connector.GenerateUniqueIds("transfer_id", _testIterations);
            }
            catch (Exception)
            {
                // ignore (means testing with non persistent server)
            }
            

            var rand = new Random();

            int failed = 0;

            Header($"creating data transfers between random accounts and update the source and destination account in a transaction");
            var sw = new Stopwatch();

            sw.Start();

            for (int i = 0; i < _testIterations; i++)
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

            Console.WriteLine($"{_testIterations} transactions took {sw.ElapsedMilliseconds} milliseconds. Rolled back transactions: {failed}. ");
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