﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Client.Core
{
    public class QueryExecutionPlan
    {
        public string Query { get; set; }

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
        public int PlanningTimeInMicroseconds { get;  set; }
            
        /// <summary>
        /// Time to process the indexes
        /// </summary>
        public int IndexTimeInMicroseconds { get;  set; }
            
        /// <summary>
        /// Time to process non-index-able part
        /// </summary>
        public int ScanTimeInMicroseconds{ get;  set; }
        

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
}