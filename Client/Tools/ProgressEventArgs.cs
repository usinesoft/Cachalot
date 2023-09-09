using System;

namespace Client.Tools;

public class ProgressEventArgs : EventArgs
{
    public ProgressEventArgs(string message)
    {
        Message = message;
    }

    public string Message { get; }
}