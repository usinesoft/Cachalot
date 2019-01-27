using System.IO;
using Client;
using Client.Profiling;

namespace AdminConsole
{
    class ProfileOutput : IProfilerLogger
    {
        private readonly TextWriter _writer;

        public ProfileOutput(TextWriter writer)
        {
            _writer = writer;
        }

        public bool IsProfilingActive { get; set; } = true;


        #region IProfilerOutput Members

        public void Write(string actionName, ActionType actionType, string format, params object[] parameters)
        {
            if ((actionType == ActionType.EndSingle) || (actionType == ActionType.EndMultiple))
            {
                string msg = string.Format(format, parameters);
                string line = actionName + ": " + msg;
                _writer.WriteLine(line);
            }
        }

        #endregion
    }
}