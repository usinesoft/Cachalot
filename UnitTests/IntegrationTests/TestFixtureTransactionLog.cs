using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Server.Persistence;

namespace Tests.IntegrationTests
{
    [TestFixture]
    public class TestFixtureTransactionLog
    {
        [SetUp]
        public void SetUp()
        {
            if (File.Exists(Path.Combine(Constants.DataPath, TransactionLog.LogFileName)))
                File.Delete(Path.Combine(Constants.DataPath, TransactionLog.LogFileName));
        }

        [Test]
        public void An_empty_log_is_blocking_consumers_and_releases_them_when_disposed()
        {
            var log = new TransactionLog();

            var workerFinished = false;
            var worker = new Thread(() =>
            {
                var transaction = log.StartProcessing();

                // when released by a dispose the result is empty
                ClassicAssert.IsNull(transaction);

                workerFinished = true;
            });

            worker.Start();

            Thread.Sleep(300);

            // check that the worker is blocked
            ClassicAssert.IsFalse(workerFinished);

            log.Dispose();

            worker.Join();

            workerFinished = true;
        }

        [Test]
        public void An_empty_log_is_blocking_consumers_and_releases_them_when_transactions_are_added()
        {
            var log = new TransactionLog();

            var workerFinished = false;

            var processed = 0;

            var worker = new Thread(() =>
            {
                while (true)
                {
                    var transaction = log.StartProcessing();

                    if (transaction != null)
                    {
                        log.EndProcessing(transaction);
                        processed++;
                    }
                    else
                    {
                        workerFinished = true;
                        return;
                    }
                }
            });

            worker.Start();

            Thread.Sleep(300);

            // check that the worker is blocked
            ClassicAssert.IsFalse(workerFinished);

            log.NewTransaction(new byte[] { 1, 2, 3 });
            Thread.Sleep(100);
            ClassicAssert.AreEqual(1, processed);
            ClassicAssert.IsFalse(workerFinished);

            log.NewTransaction(new byte[] { 2, 3, 4 });
            Thread.Sleep(100);
            ClassicAssert.AreEqual(2, processed);
            ClassicAssert.IsFalse(workerFinished);

            log.Dispose();

            worker.Join();

            workerFinished = true;
        }

        [Test]
        public void Clear_log_when_everything_is_processed()
        {
            var t1 = new byte[] { 1, 2, 3 };
            var t2 = new byte[] { 11, 12, 13, 14, 15, 16 };
            var t3 = new byte[] { 100, 200, 210, 220 };

            var log = new TransactionLog();


            {
                var sw = new Stopwatch();

                sw.Start();

                for (var i = 0; i < 1000; i++)
                {
                    log.NewTransaction(t1);
                    log.NewTransaction(t2);
                    log.NewTransaction(t3);
                }

                sw.Stop();

                Console.WriteLine($"Writing 3000 transactions took {sw.ElapsedMilliseconds} milliseconds");

                log.Dispose();
            }


            //reload
            log = new TransactionLog();

            // process all the transactions
            ClassicAssert.AreEqual(3000, log.PendingTransactionsCount);

            {
                var sw = new Stopwatch();

                sw.Start();
                for (var i = 0; i < 3000; i++)
                {
                    var t = log.StartProcessing();
                    log.EndProcessing(t);
                }

                sw.Stop();

                Console.WriteLine($"Processing 3000 transactions took {sw.ElapsedMilliseconds} milliseconds");
            }

            log.ClearLog();
            log.Dispose();

            Console.WriteLine(log.FileAsString());


            //reload
            log = new TransactionLog();
            ClassicAssert.AreEqual(0, log.PendingTransactionsCount);

            // check that is still works after cleanup
            log.NewTransaction(t1);
            log.NewTransaction(t2);
            log.NewTransaction(t3);
            var tr = log.StartProcessing();
            log.EndProcessing(tr);

            log.Dispose();

            log = new TransactionLog();
            ClassicAssert.AreEqual(2, log.PendingTransactionsCount);
            log.Dispose();

            Console.WriteLine(log.FileAsString());
        }


        [Test]
        public void Performance_test()
        {
            var data = new byte[1000];
            for (var i = 0; i < 1000; i++) data[i] = (byte)(i % 255);

            using (var log = new TransactionLog())
            {
                var sw = new Stopwatch();

                sw.Start();

                for (var i = 0; i < 10000; i++) log.NewTransaction(data);

                sw.Stop();

                Console.WriteLine($"Writing 10000 transactions took {sw.ElapsedMilliseconds} milliseconds");
            }
        }

        [Test]
        public void Test_cancel_delayed_transactions()
        {
            var t1 = new byte[] { 1, 2, 3 };

            var log = new TransactionLog();

            log.NewTransaction(t1, 10);

            log.CancelTransaction();

            ClassicAssert.AreEqual(0, log.PendingTransactionsCount);

            log.Dispose();

            Console.WriteLine(log.FileAsString());

            // canceled transactions should not be reloaded
            log = new TransactionLog();
            ClassicAssert.AreEqual(0, log.PendingTransactionsCount);

            // cancel a delayed transaction while it is processed
            log.NewTransaction(t1, 100);
            Task.Factory.StartNew(() =>
            {
                Thread.Sleep(10);
                log.CancelTransaction();
            });

            try
            {
                var transaction = log.StartProcessing();

                ClassicAssert.IsNull(transaction);
            }
            finally
            {
                log.Dispose();
            }
        }

        [Test]
        public void Test_delayed_transactions()
        {
            var t1 = new byte[] { 1, 2, 3 };

            var log = new TransactionLog();

            log.NewTransaction(t1, 100);

            var transaction = log.StartProcessing();

            ClassicAssert.IsTrue(DateTime.Now - transaction.TimeStamp > TimeSpan.FromMilliseconds(10),
                "The transaction log should wait for a delayed transaction");

            log.EndProcessing(transaction);

            log.Dispose();

            Console.WriteLine(log.FileAsString());

            log = new TransactionLog();
            ClassicAssert.AreEqual(0, log.PendingTransactionsCount);
            log.Dispose();
        }

        [Test]
        public void Test_persistent_transaction_log()
        {
            var t1 = new byte[] { 1, 2, 3 };
            var t2 = new byte[] { 11, 12, 13, 14, 15, 16 };

            var log = new TransactionLog();

            log.NewTransaction(t1);
            log.NewTransaction(t2);

            log.Dispose();

            Console.WriteLine(log.FileAsString());

            //reload
            log = new TransactionLog();

            ClassicAssert.AreEqual(2, log.PendingTransactionsCount);

            var data1 = log.StartProcessing();
            log.EndProcessing(data1);

            ClassicAssert.AreEqual(1, log.PendingTransactionsCount);

            log.Dispose();
            Console.WriteLine(log.FileAsString());

            //reload
            log = new TransactionLog();
            ClassicAssert.AreEqual(1, log.PendingTransactionsCount);

            log.Dispose();
        }
    }
}