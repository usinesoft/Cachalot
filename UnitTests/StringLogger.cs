using System;
using Client.Core;

namespace UnitTests
{
    /// <summary>
    ///     Allows the command processing system to log to a string for test purpose
    /// </summary>
    internal class StringLogger : ICommandLogger
    {
        public string Buffer { get; private set; }

        public void Write(string message)
        {
            Buffer += message + Environment.NewLine;
        }

        public void Write(string format, params object[] parameters)
        {
            var line = string.Format(format, parameters);
            Buffer += line + Environment.NewLine;
        }

        public void WriteError(string message)
        {
            Buffer += message + Environment.NewLine;
        }

        #region ICommandLogger Members

        public void WriteError(string format, params object[] parameters)
        {
            var line = string.Format(format, parameters);
            Buffer += line + Environment.NewLine;
        }

        #endregion

        public void Reset()
        {
            Buffer = string.Empty;
        }
    }
}