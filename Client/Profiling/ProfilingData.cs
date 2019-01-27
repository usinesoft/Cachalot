using System;
using System.Diagnostics;

namespace Client.Profiling
{
    /// <summary>
    ///     Profiling data / action / thread
    ///     Same action in different threads uses different instances so no syncronisation is needed
    /// </summary>
    public class ProfilingData
    {
        private readonly IProfilerLogger _logger;


        /// <summary>
        ///     The one and only stopwatch (started and stopped by the Profiler class)
        ///     Read only access from all instances of this class, so no lock is needed
        /// </summary>
        private readonly Stopwatch _parentStopwatch;

        private int _count;

        private State _state = State.NotStarted;

        /// <summary>
        ///     Processor ticks (to be filled when the action is started)
        /// </summary>
        private long _ticksAtStart;

        internal ProfilingData(bool isMultiple, string action, IProfilerLogger logger, Stopwatch stopwatch)
        {
            IsMultiple = isMultiple;
            Action = action;
            _parentStopwatch = stopwatch;
            _logger = logger;
        }

        /// <summary>
        ///     Total execution time (available only when the action is finished)
        /// </summary>
        public double TotalTimeMiliseconds { get; private set; }

        public double MinTimeMiliseconds { get; private set; } = double.MaxValue;

        public double MaxTimeMiliseconds { get; private set; } = double.MinValue;

        public double AvgTimeMiliseconds { get; private set; }

        public bool IsMultiple { get; }

        public string Action { get; }

        internal void StartSingle()
        {
            if (_state != State.NotStarted)
                throw new NotSupportedException("already started");

            if (IsMultiple)
                throw new NotSupportedException("not a single action");

            _count++;
            _state = State.StartedSingle;
            _ticksAtStart = _parentStopwatch.ElapsedTicks;
        }

        internal void EndSingle()
        {
            if (_state != State.StartedSingle)
                throw new NotSupportedException();

            _state = State.NotStarted;

            var ticksNow = _parentStopwatch.ElapsedTicks;
            TotalTimeMiliseconds = getMiliseconds(_ticksAtStart, ticksNow);

            if (_logger != null)
                _logger.Write(Action, ActionType.EndSingle, "took {0} miliseconds",
                    TotalTimeMiliseconds.ToString("F4"));
        }

        internal void StartMultiple()
        {
            if (_state != State.NotStarted)
                throw new NotSupportedException("already started");

            if (IsMultiple == false)
                throw new NotSupportedException("not a multiple action");

            _count = 0;
            TotalTimeMiliseconds = 0;
            MinTimeMiliseconds = double.MaxValue;
            MaxTimeMiliseconds = double.MinValue;

            _state = State.StartedMany;
        }

        internal void EndMultiple()
        {
            if (_state != State.StartedMany)
                throw new NotSupportedException();

            _state = State.NotStarted;

            AvgTimeMiliseconds = TotalTimeMiliseconds / _count;

            if (_logger != null)
                _logger.Write(Action, ActionType.EndMultiple,
                    "{0} calls took {1} miliseconds\n \tavg ms/call = {2}\n \tmin ms/call = {3}\n \tmax ms/call = {4}",
                    _count, TotalTimeMiliseconds.ToString("F4"), AvgTimeMiliseconds.ToString("F4"),
                    MinTimeMiliseconds.ToString("F4"), MaxTimeMiliseconds.ToString("F4"));
        }

        internal void StartOne()
        {
            if (_state != State.StartedMany)
                throw new NotSupportedException("calling StartOne() before StartMany()");

            _count++;
            _state = State.StartedOne;
            _ticksAtStart = _parentStopwatch.ElapsedTicks;
        }

        internal void EndOne()
        {
            if (_state != State.StartedOne)
                throw new NotSupportedException();

            _state = State.StartedMany;


            var ticksNow = _parentStopwatch.ElapsedTicks;
            var myTime = getMiliseconds(_ticksAtStart, ticksNow);


            if (myTime < MinTimeMiliseconds)
                MinTimeMiliseconds = myTime;

            if (myTime > MaxTimeMiliseconds)
                MaxTimeMiliseconds = myTime;

            TotalTimeMiliseconds = TotalTimeMiliseconds + myTime;


            if (_logger != null)
                _logger.Write(Action, ActionType.EndOne, "one step took {1} miliseconds", myTime.ToString("F4"));
        }


        /// <summary>
        ///     Convert processor ticks to miliseconds
        /// </summary>
        /// <param name="startTicks"></param>
        /// <param name="endTicks"></param>
        /// <returns></returns>
        private static double getMiliseconds(long startTicks, long endTicks)
        {
            var elapsedSeconds = (endTicks - (double) startTicks) / Stopwatch.Frequency;
            return elapsedSeconds * 1000D;
        }


        private enum State
        {
            NotStarted,
            StartedSingle,
            StartedMany,
            StartedOne
        }
    }
}