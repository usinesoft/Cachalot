using System;
using System.Text;

namespace Client.Interface
{
    /// <summary>
    ///     Cache exception. May be client or server generated
    /// </summary>
    public class CacheException : Exception
    {
        /// <summary>
        ///     Pure client-side exception
        /// </summary>
        /// <param name="message"></param>
        /// <param name="exceptionType"></param>
        public CacheException(string message, ExceptionType exceptionType = ExceptionType.Unknown) : this(message, "",
            "")
        {
            ExceptionType = exceptionType;
        }

        /// <summary>
        ///     Wrap a server exception in an exception to be thrown on the client side
        /// </summary>
        /// <param name="message"></param>
        /// <param name="serverMessage"></param>
        /// <param name="serverStack"></param>
        /// <param name="exceptionType"></param>
        public CacheException(string message, string serverMessage, string serverStack,
            ExceptionType exceptionType = ExceptionType.Unknown) : base(message)
        {
            ServerStack = serverStack;
            ExceptionType = exceptionType;
            ServerMessage = serverMessage;
        }

        /// <summary>
        ///     The original message from the exception raised on the server
        /// </summary>
        public string ServerMessage { get; internal set; }

        /// <summary>
        ///     The original call stack from the exception raised on the server
        /// </summary>
        public string ServerStack { get; internal set; }

        public override string Message => base.Message + "(" + ServerMessage + ")";

        public ExceptionType ExceptionType { get; }

        public bool IsTransactionException => ExceptionType != ExceptionType.Unknown;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"exception type:{ExceptionType}");
            sb.AppendLine("Client exception:");
            sb.AppendLine(Message);
            sb.AppendLine(StackTrace);

            sb.AppendLine("Server exception:");
            sb.AppendLine(ServerMessage);
            sb.AppendLine(ServerStack);

            return sb.ToString();
        }
    }
}