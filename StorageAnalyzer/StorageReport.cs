﻿using System.Collections.Generic;
using System.Linq;
using System.Text;
using Server.Persistence;

namespace StorageAnalyzer
{
    public class StorageReport
    {
        public int TotalBlocks { get; set; }
        public int ActiveBlocks { get; set; }
        public int DeletedBlocks { get; set; }
        public int DirtyBlocks { get; set; }
        public int InvalidBlocks { get; set; }
        public int LastTransactionId { get; set; }

        public string LastPrimaryKey { get; set; }

        public List<string> DuplicatePrimaryKeys { get; } = new List<string>();


        public List<PersistentBlock> BlocksWithIssues { get; } = new List<PersistentBlock>();
        public long Size { get; set; }

        public override string ToString()
        {
            var result = new StringBuilder();

            result.AppendLine($"size               : {Size:N0}");
            result.AppendLine($"total blocks       : {TotalBlocks:N0}");
            result.AppendLine($"active blocks      : {ActiveBlocks:N0}");
            result.AppendLine($"deleted blocks     : {DeletedBlocks:N0}");
            result.AppendLine($"dirty blocks       : {DirtyBlocks:N0}");
            result.AppendLine($"invalid blocks     : {InvalidBlocks:N0}");
            result.AppendLine($"last transaction   : {LastTransactionId}");
            result.AppendLine($"last pk            : {LastPrimaryKey}");


            return result.ToString();
        }

        public string Analysis()
        {
            var result = new StringBuilder();

            if (BlocksWithIssues.Count > 0)
            {
                result.AppendLine("blocks with issues:");
                foreach (var blockWithIssue in BlocksWithIssues) result.AppendLine("  " + blockWithIssue);

                result.AppendLine();
            }

            if (DuplicatePrimaryKeys.Any())
            {
                result.AppendLine($"{DuplicatePrimaryKeys.Count} Duplicate primary keys:");
                foreach (var key in DuplicatePrimaryKeys.OrderBy(x => x)) result.AppendLine($"{key}");
            }

            return result.ToString();
        }
    }
}