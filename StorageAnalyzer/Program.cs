using System;
using System.Collections.Generic;
using System.IO;
using Server.Persistence;

namespace StorageAnalyzer
{
    internal partial class Program
    {
        private static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.WriteLine(Logo);

            if (args.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Usage: storageanalyzer data_directory ");
                return;
            }

            var dir = args[0];
            if (!Directory.Exists(dir))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Directory not found {dir}");
                return;
            }


            Console.WriteLine("analyzing the transaction log...");
            Console.WriteLine();

            var report = AnalyzeTransactionLog(dir);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(report.ToString());

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(report.Analysis());

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("analyzing the persistent storage...");
            Console.WriteLine();

            var report1 = AnalyzeStorage(dir);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(report1.ToString());

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(report1.Analysis());

            Console.ForegroundColor = ConsoleColor.White;

            if (report1.BlocksWithIssues.Count > 0)
            {
                Console.WriteLine("There are invalid blocks in the storage. Fix it? (y/n)");
                var key = Console.ReadKey();
                if (key.KeyChar == 'y' || key.KeyChar == 'Y')
                {
                    Console.WriteLine("Start compacting and repairing storage...");
                    ReliableStorage.CompactAndRepair(dir, ReliableStorage.StorageFileName,
                        ReliableStorage.TempFileName);
                    Console.WriteLine("done");
                }
            }
        }


        private static StorageReport AnalyzeStorage(string dir)
        {
            var fullPath = Path.Combine(dir, ReliableStorage.StorageFileName);

            using var storage = new FileStream(fullPath, FileMode.Open);

            storage.Seek(0, SeekOrigin.Begin);

            var block = new PersistentBlock();

            var reader = new BinaryReader(storage);

            var storageReport = new StorageReport();

            var index = 0;

            var primaryKeys = new HashSet<string>();

            // read all the blocks
            while (block.Read(reader, true))
            {
                storageReport.TotalBlocks++;

                if (!block.IsValidBlock())
                {
                    storageReport.BlocksWithIssues.Add(block);
                    storageReport.InvalidBlocks++;
                }

                if (block.IsValidBlock() && !primaryKeys.Add(block.PrimaryKey))
                    storageReport.DuplicatePrimaryKeys.Add(block.PrimaryKey);

                if (block.BlockStatus == BlockStatus.Active) storageReport.ActiveBlocks++;

                if (block.BlockStatus == BlockStatus.Deleted) storageReport.DeletedBlocks++;

                if (block.BlockStatus == BlockStatus.Dirty) storageReport.DirtyBlocks++;

                if (!block.IsValidBlock())
                    ReliableStorage.FindNextBeginMarker(storage.Position + PersistentBlock.MinSize, storage);

                // get the most recent transaction committed to this storage
                if (block.LastTransactionId > storageReport.LastTransactionId)
                    storageReport.LastTransactionId = block.LastTransactionId;

                storageReport.LastPrimaryKey = block.PrimaryKey;

                index++;
                block = new PersistentBlock { Index = index };
            }

            storageReport.Size = storage.Position;

            return storageReport;
        }

        private static TransactionLogReport AnalyzeTransactionLog(string dir)
        {
            var fullPath = Path.Combine(dir, TransactionLog.LogFileName);

            using var transactionLog = new FileStream(fullPath, FileMode.Open);

            transactionLog.Seek(0, SeekOrigin.Begin);

            using var reader = new BinaryReader(transactionLog);


            var report = new TransactionLogReport { LastOffset = reader.ReadInt64() };

            var stop = false;
            while (!stop)
                try
                {
                    var transactionData = TransactionLog.ReadTransaction(reader);

                    report.TransactionCount++;

                    switch (transactionData.TransactionStatus)
                    {
                        case TransactionStatus.ToProcess:
                            report.ToProcessCount++;
                            break;
                        case TransactionStatus.Processing:
                            report.ProcessingCount++;
                            report.ProcessingTransactions.Add(transactionData);
                            break;
                        case TransactionStatus.Processed:
                            report.ProcessedCount++;
                            break;
                        case TransactionStatus.Canceled:
                            report.CanceledCount++;
                            break;
                    }

                    if (!transactionData.EndMarkerOk) report.TransactionsWithIssues.Add(transactionData);
                }
                catch (EndOfStreamException)
                {
                    report.LastOffsetFound = transactionLog.Position;
                    stop = true;
                }

            return report;
        }
    }
}