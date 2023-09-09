using System;

namespace Client.Core;

public class StreamingException : Exception
{
    public StreamingException(string message) : base(message)
    {
    }


    public StreamingException(string message, string stackTrace) : base(message)
    {
        StackTrace = stackTrace;
    }


    public override string StackTrace { get; }
}