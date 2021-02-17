using System;
using System.Diagnostics;

namespace AdminConsole.Commands
{
    public class ConsoleProfiler
    {
        private string _currentAction;

        readonly Stopwatch _watch = new Stopwatch();

        public bool IsActive { get; set; } = false;

        public void Start(string action)
        {
            _currentAction = action;

            _watch.Restart();
        }

        public void End()
        {
            _watch.Stop();

            if (IsActive)
            {
                Console.WriteLine($"{_currentAction} took {_watch.ElapsedMilliseconds} ms");
            }
        }


        public long TotalTimeMilliseconds => _watch.ElapsedMilliseconds;
    }
}