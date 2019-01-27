using System;
using System.Runtime.Serialization;

namespace Client.Core
{
    public class StreamingException : Exception
    {
        public StreamingException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public StreamingException(string message) : base(message)
        {
        }


        public StreamingException(string message, string stackTrace) : base(message)
        {
            StackTrace = stackTrace;
        }


        public override string StackTrace { get; }
    }
}