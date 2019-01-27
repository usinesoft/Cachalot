using System;
using Client.Interface;
using ProtoBuf;

namespace Client.ChannelInterface
{
    /// <summary>
    ///     An exception occured on the server while processing the request
    ///     As <see cref="Exception" /> derived classes can not be guaranteed to be serializable, the useful
    ///     exception information is transferred by this class (which is not <see cref="Exception" /> derived)
    /// </summary>
    [ProtoContract]
    public class ExceptionResponse : Response
    {
        [ProtoMember(1)] private readonly string _callStack;
        [ProtoMember(3)] private ExceptionType _exceptionType;
        [ProtoMember(2)] private readonly string _message;

        /// <summary>
        ///     Required by protocol buffers
        /// </summary>
        public ExceptionResponse()
        {
        }

        /// <summary>
        ///     Create from exception. Only the message and the call stack are copied from
        ///     the original exception
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="exceptionType"></param>
        public ExceptionResponse(Exception ex, ExceptionType exceptionType = ExceptionType.Unknown)
        {
            _exceptionType = exceptionType;
            _callStack = ex.ToString();
            _message = ex.Message;
        }

        public override ResponseType ResponseType => ResponseType.Exception;

        /// <summary>
        ///     Server side call stack
        /// </summary>
        public string CallStack => _callStack;

        /// <summary>
        ///     Server side exception message
        /// </summary>
        public string Message => _message;

        public ExceptionType ExceptionType
        {
            get => _exceptionType;
            set => _exceptionType = value;
        }
    }
}