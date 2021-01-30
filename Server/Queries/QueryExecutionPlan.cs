using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Server.Queries
{
    public class QueryExecutionPlan
    {
        public string Query { get; }

        public QueryExecutionPlan(string query)
        {
            Query = query;
        }


        readonly Stopwatch _watch = new Stopwatch();
            
        

        /// <summary>
        /// If true, simplified execution for an atomic queries
        /// </summary>
        public bool SimpleQueryStrategy { get; set; }
            
        public bool FullScan{ get; set; }

        /// <summary>
        /// Time to choose the indexes
        /// </summary>
        public int PlanningTimeInMicroseconds { get; private set; }
            
        /// <summary>
        /// Time to process the indexes
        /// </summary>
        public int IndexTimeInMicroseconds { get; private set; }
            
        /// <summary>
        /// Time to process non-index-able part
        /// </summary>
        public int ScanTimeInMicroseconds{ get; private set; }
        

        readonly List<string> _traces = new List<string>();

        public void Trace(string message)
        {
            _traces.Add(message);
        }


        public void StartPlanning()
        {
            _watch.Restart();
        }

        public void EndPlanning()
        {
            _watch.Stop();

            PlanningTimeInMicroseconds = (int) (_watch.Elapsed.TotalMilliseconds * 1000D);
        }

        public void StartIndexUse()
        {
            _watch.Restart();
        }

        public void EndIndexUse()
        {
            _watch.Stop();

            IndexTimeInMicroseconds = (int) (_watch.Elapsed.TotalMilliseconds * 1000D);
        }

        public void StartScan()
        {
            _watch.Restart();
        }

        public void EndScan()
        {
            _watch.Stop();

            ScanTimeInMicroseconds = (int) (_watch.Elapsed.TotalMilliseconds * 1000D);
        }

        
        public override string ToString()
        {
            var result = new StringBuilder();

            result.AppendLine(Query);
                
            result.AppendLine($"{nameof(SimpleQueryStrategy)}: {SimpleQueryStrategy}, {nameof(FullScan)}: {FullScan}, PlanningTime: {PlanningTimeInMicroseconds} μs, IndexTime: {IndexTimeInMicroseconds} μs, ScanTime: {ScanTimeInMicroseconds} μs");

            foreach (var trace in _traces)
            {
                result.AppendLine(trace);
            }

            return result.ToString();

        }


     
    }


    /// <summary>
    /// A global execution plan. Includes one or more query plans and postprocessing time (order by, distinct)
    /// </summary>
    public class ExecutionPlan
    {

        readonly Stopwatch _watch = new Stopwatch();

        public int TotalTimeInMicroseconds{ get; private set; }

        public void Begin()
        {
            _watch.Start();
        }

        public void End()
        {
            _watch.Stop();

            TotalTimeInMicroseconds = (int) (_watch.Elapsed.TotalMilliseconds * 1000D);
        }

        public IList<QueryExecutionPlan> QueryPlans { get; } = new List<QueryExecutionPlan>();

        /// <summary>
        /// Tie to sort the result (if order-by clause is present)
        /// </summary>
        public int SortTimeInMicroseconds { get; private set; }
            
        /// <summary>
        /// Time to execute the distinct clause (if present)
        /// </summary>
        public int DistinctTimeInMicroseconds { get; private set; }

        /// <summary>
        /// Time to merge sub-queries (if more than one)
        /// </summary>
        public int MergeTimeInMicroseconds { get; private set; }
        
        /// <summary>
        /// Time to process a full-text query
        /// </summary>
        public int FulltextSearchTimeInMicroseconds { get; private set; }

        public override string ToString()
        {
            var result = new StringBuilder();

            result.AppendLine($"Total execution time : {TotalTimeInMicroseconds} μs");

            if (MergeTimeInMicroseconds != 0)
            {
                result.AppendLine($"Merge time : {MergeTimeInMicroseconds} μs");
            }

            if (FulltextSearchTimeInMicroseconds != 0)
            {
                result.AppendLine($"Full-text search time : {FulltextSearchTimeInMicroseconds} μs");
            }

            if (DistinctTimeInMicroseconds != 0)
            {
                result.AppendLine($"Distinct time : {DistinctTimeInMicroseconds} μs");
            }

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

            DistinctTimeInMicroseconds = (int) ((elapsed.TotalMilliseconds - _startDistinct.TotalMilliseconds) * 1000);

        }

        private TimeSpan _startMerge;
        public void BeginMerge()
        {
            _startMerge = _watch.Elapsed;
        }

        public void EndMerge()
        {
            var elapsed = _watch.Elapsed;

            MergeTimeInMicroseconds = (int) ((elapsed.TotalMilliseconds - _startMerge.TotalMilliseconds) * 1000);

        }

        private TimeSpan _startFullText;
        public void BeginFullText()
        {
            _startFullText = _watch.Elapsed;
        }

        public void EndFullText()
        {
            var elapsed = _watch.Elapsed;

            FulltextSearchTimeInMicroseconds = (int) ((elapsed.TotalMilliseconds - _startFullText.TotalMilliseconds) * 1000);

        }
    }
}