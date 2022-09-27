using System;

namespace Client.Tools
{
    public class ProgressEventArgs : EventArgs
    {
        public string Message { get; }

        public ProgressEventArgs(string message)
        {
            Message = message;
        }
    }
}
