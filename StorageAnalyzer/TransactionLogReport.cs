using System.Collections.Generic;
using System.Text;
using Server.Persistence;

namespace StorageAnalyzer
{
    public class TransactionLogReport
    {
        public long LastOffset { get; set; }

        public long LastOffsetFound { get; set; }

        public long TransactionCount { get; set; }

        public long ProcessingCount { get; set; }

        public long CanceledCount { get; set; }

        public long ToProcessCount { get; set; }

        public long ProcessedCount { get; set; }
        


        public List<TransactionLog.TransactionData> ProcessingTransactions { get; } = new List<TransactionLog.TransactionData>();

        public List<TransactionLog.TransactionData> TransactionsWithIssues { get; } = new List<TransactionLog.TransactionData>();

        public string Analysis()
        {
            var result = new StringBuilder();

            if (TransactionsWithIssues.Count > 0)
            {
                result.AppendLine("transactions with issues:");
                foreach (var transactionsWithIssue in TransactionsWithIssues)
                {
                    result.AppendLine("  " + transactionsWithIssue.ToString());
                }

                result.AppendLine();
            }

            if (ProcessingTransactions.Count > 0)
            {
             
                result.AppendLine("transactions not fully committed:");

                foreach (var transactionsWithIssue in ProcessingTransactions)
                {
                    result.AppendLine("  "+transactionsWithIssue);
                }

                result.AppendLine();
            }

            return result.ToString();
        }

        public override string ToString()
        {
            var result = new StringBuilder();

            
            result.AppendLine( $"last offset       : {LastOffset:N0}");
            result.AppendLine( $"last offset found : {LastOffsetFound:N0}");
            result.AppendLine( $"total transactions: {TransactionCount:N0}");
            result.AppendLine( $"processing        : {ProcessingCount:N0}");
            result.AppendLine( $"canceled          : {CanceledCount:N0}");
            result.AppendLine( $"to be processed   : {ToProcessCount:N0}");
            result.AppendLine( $"already processed : {ProcessedCount:N0}");

            return result.ToString();
        }
    }
}