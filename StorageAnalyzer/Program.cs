using Server.Persistence;
using System;
using System.IO;

namespace StorageAnalyzer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.WriteLine("------------------------------------------------------");
            Console.WriteLine("|     Persistent store and transaction log analyzer  |");
            Console.WriteLine("------------------------------------------------------");
            Console.WriteLine();

            if (args.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Usage storageanalyzer data_directory ");
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
        }

        static bool IsValidBlock(PersistentBlock block)
        {

            if (block.BeginMarker != PersistentBlock.BeginMarkerValue)
            {
                return false;
            }

            if (block.EndMarker != PersistentBlock.EndMarkerValue)
            {
                return false;
            }


            return block.HashOk;
        }

        private static long FindNextBeginMarker(long offset, Stream stream)
        {
            var reader = new BinaryReader(stream);
            for (var curOffset = offset; ; curOffset++)
                try
                {
                    stream.Seek(curOffset, SeekOrigin.Begin);
                    var marker = reader.ReadInt32();
                    if (marker == PersistentBlock.BeginMarkerValue) return curOffset;
                }
                catch (EndOfStreamException)
                {
                    return curOffset;
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

            // read all the blocks
            while (block.Read(reader, true))
            {
                storageReport.TotalBlocks++;

                if (!IsValidBlock(block))
                {
                    storageReport.BlocksWithIssues.Add(block);
                    storageReport.InvalidBlocks++;
                }

                if (block.BlockStatus == BlockStatus.Active)
                {
                    storageReport.ActiveBlocks++;
                }

                if (block.BlockStatus == BlockStatus.Deleted)
                {
                    storageReport.DeletedBlocks++;
                }

                if (block.BlockStatus == BlockStatus.Dirty)
                {
                    storageReport.DirtyBlocks++;
                }

                if (!IsValidBlock(block))
                {
                    FindNextBeginMarker(storage.Position + PersistentBlock.MinSize, storage);
                }

                // get the most recent transaction committed to this storage
                if (block.LastTransactionId > storageReport.LastTransactionId)
                {
                    storageReport.LastTransactionId = block.LastTransactionId;
                }

                storageReport.LastPrimaryKey = block.PrimaryKey;

                block = new PersistentBlock();
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
            {
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

                    if (!transactionData.EndMarkerOk)
                    {
                        report.TransactionsWithIssues.Add(transactionData);
                    }


                }
                catch (EndOfStreamException)
                {

                    report.LastOffsetFound = transactionLog.Position;
                    stop = true;
                }
            }

            return report;
        }
    }
}
