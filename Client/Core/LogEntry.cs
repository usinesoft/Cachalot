using Client.Interface;
using System;

namespace Client.Core
{

    /// <summary>
    /// The server log is a special table called @ACTIVITY
    /// This class is an entry from this table
    /// </summary>
    public class LogEntry
    {

        public static readonly string Table = "@ACTIVITY";

        #region entry types

        public static readonly string Select = "SELECT";
        public static readonly string Delete = "DELETE";
        public static readonly string Put = "PUT";
        public static readonly string Eval = "COUNT";

        #endregion

        [ServerSideValue(IndexType.Primary)]
        public Guid Id { get; set; }

        [ServerSideValue(IndexType.Ordered)]
        public DateTimeOffset TimeStamp { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public string Type { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public string CollectionName { get; set; }

        [ServerSideValue(IndexType.Ordered)]
        public int ExecutionTimeInMicroseconds { get; set; }

        [ServerSideValue(IndexType.None)]
        public string Detail { get; set; }

        [ServerSideValue(IndexType.None)]
        public string Query { get; set; }


        /// <summary>
        /// Filled only for commands that imply queries
        /// </summary>

        public ExecutionPlan ExecutionPlan { get; set; }

        public override string ToString()
        {
            return $"{nameof(Type)}: {Type}, {nameof(ExecutionTimeInMicroseconds)}: {ExecutionTimeInMicroseconds}, {nameof(Detail)}: {Detail}, {nameof(Query)}: {Query}, {nameof(ExecutionPlan)}: {ExecutionPlan}";
        }
    }
}