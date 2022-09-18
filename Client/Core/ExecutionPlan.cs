using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Client.Core
{
    /// <summary>
    /// A global execution plan. Includes one or more query plans and postprocessing time (order by, distinct)
    /// </summary>
    public class ExecutionPlan
    {

        readonly Stopwatch _watch = new Stopwatch();

        public int TotalTimeInMicroseconds { get; set; }

        public void Begin()
        {
            _watch.Start();
        }

        public void End()
        {
            _watch.Stop();

            TotalTimeInMicroseconds = (int)(_watch.Elapsed.TotalMilliseconds * 1000D);
        }

        public IList<QueryExecutionPlan> QueryPlans { get; set; } = new List<QueryExecutionPlan>();

        /// <summary>
        /// Time to execute the distinct clause (if present)
        /// </summary>
        public int DistinctTimeInMicroseconds { get; set; }

        /// <summary>
        /// Time to merge sub-queries (if more than one)
        /// </summary>
        public int MergeTimeInMicroseconds { get; set; }


        /// <summary>
        /// Order time in microseconds
        /// </summary>
        public int OrderTimeInMicroseconds { get; set; }

        public override string ToString()
        {
            var result = new StringBuilder();

            result.AppendLine($"Total execution time : {TotalTimeInMicroseconds} μs");

            if (OrderTimeInMicroseconds != 0)
            {
                result.AppendLine($"Order-by time : {OrderTimeInMicroseconds} μs");
            }

            if (DistinctTimeInMicroseconds != 0)
            {
                result.AppendLine($"Distinct time : {DistinctTimeInMicroseconds} μs");
            }

            if (MergeTimeInMicroseconds != 0)
            {
                result.AppendLine($"Merge time : {MergeTimeInMicroseconds} μs");
            }

            result.AppendLine();

            foreach (var queryPlan in QueryPlans)
            {
                result.AppendLine(queryPlan.ToString());
            }

            return result.ToString();
        }

        private TimeSpan _startDistinct;
        public void BeginDistinct()
        {
            _startDistinct = _watch.Elapsed;
        }

        public void EndDistinct()
        {
            var elapsed = _watch.Elapsed;

            DistinctTimeInMicroseconds = (int)((elapsed.TotalMilliseconds - _startDistinct.TotalMilliseconds) * 1000);

        }

        private TimeSpan _startMerge;
        public void BeginMerge()
        {
            _startMerge = _watch.Elapsed;
        }

        public void EndMerge()
        {
            var elapsed = _watch.Elapsed;

            MergeTimeInMicroseconds = (int)((elapsed.TotalMilliseconds - _startMerge.TotalMilliseconds) * 1000);

        }


        private TimeSpan _startOrderBy;
        public void BeginOrderBy()
        {
            _startOrderBy = _watch.Elapsed;
        }

        public void EndOrderBy()
        {
            var elapsed = _watch.Elapsed;

            OrderTimeInMicroseconds = (int)((elapsed.TotalMilliseconds - _startOrderBy.TotalMilliseconds) * 1000);
        }
    }
}