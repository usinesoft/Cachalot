using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Client.Profiling
{
    public class Profiler
    {
        /// <summary>
        ///     The action that is currently profiled by thread id
        /// </summary>
        private readonly Dictionary<int, string> _currentThreadAction;

        private readonly Dictionary<string, ProfilingData> _profilingData;

        /// <summary>
        ///     The one and only stopwatch
        /// </summary>
        private readonly Stopwatch _stopwatch;

        private IProfilerLogger _logger;

        public Profiler()
        {
            _profilingData = new Dictionary<string, ProfilingData>();
            _currentThreadAction = new Dictionary<int, string>();
            _stopwatch = new Stopwatch();
            _stopwatch.Start();
        }

        public IProfilerLogger Logger
        {
            get => _logger;
            set
            {
                _logger = value;
                IsActive = _logger.IsProfilingActive;
            }
        }

        /// <summary>
        ///     The profiler can be activated explicitely or by the the attached <see cref="Logger" />
        /// </summary>
        public bool IsActive { private get; set; }

        private static string CreateKey(string action)
        {
            return Thread.CurrentThread.ManagedThreadId + ":" + action;
        }


        private void SetCurrentAction(string actionName)
        {
            if (_currentThreadAction.ContainsKey(Thread.CurrentThread.ManagedThreadId))
                _currentThreadAction[Thread.CurrentThread.ManagedThreadId] = actionName;
            else
                _currentThreadAction.Add(Thread.CurrentThread.ManagedThreadId, actionName);
        }

        public void Start(string action)
        {
            if (!IsActive)
                return;

            lock (_profilingData)
            {
                var key = CreateKey(action);

                if (!_profilingData.ContainsKey(key))
                    _profilingData.Add(key, new ProfilingData(false, action, _logger, _stopwatch));

                SetCurrentAction(action);

                _profilingData[key].StartSingle();
            }
        }

        public ProfilingData End(string action = null)
        {
            if (!IsActive)
                return null;

            ProfilingData data;
            lock (_profilingData)
            {
                var actionName = action ?? _currentThreadAction[Thread.CurrentThread.ManagedThreadId];
                var key = CreateKey(actionName);

                _profilingData[key].EndSingle();

                data = _profilingData[key];
                _profilingData.Remove(key);
            }

            return data;
        }

        public void StartMany(string action)
        {
            if (!IsActive)
                return;

            lock (_profilingData)
            {
                var key = CreateKey(action);

                SetCurrentAction(action);

                if (!_profilingData.ContainsKey(key))
                    _profilingData.Add(key, new ProfilingData(true, action, _logger, _stopwatch));

                _profilingData[key].StartMultiple();
            }
        }

        public ProfilingData EndMany(string action = null)
        {
            if (!IsActive)
                return null;

            lock (_profilingData)
            {
                var actionName = action ?? _currentThreadAction[Thread.CurrentThread.ManagedThreadId];
                var key = CreateKey(actionName);

                var result = _profilingData[key];
                result.EndMultiple();
                _profilingData.Remove(key);

                return result;
            }
        }

        public void StartOne()
        {
            if (!IsActive)
                return;

            lock (_profilingData)
            {
                var actionName = _currentThreadAction[Thread.CurrentThread.ManagedThreadId];
                var key = CreateKey(actionName);

                _profilingData[key].StartOne();
            }
        }

        public void EndOne()
        {
            if (!IsActive)
                return;

            lock (_profilingData)
            {
                var actionName = _currentThreadAction[Thread.CurrentThread.ManagedThreadId];
                var key = CreateKey(actionName);

                _profilingData[key].EndOne();
            }
        }
    }
}