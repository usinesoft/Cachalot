using System;
using Client.Interface;
using ProtoBuf;

namespace Client.ChannelInterface;

/// <summary>
///     An exception occured on the server while processing the request
///     As <see cref="Exception" /> derived classes can not be guaranteed to be serializable, the useful
///     exception information is transferred by this class (which is not <see cref="Exception" /> derived)
/// </summary>
[ProtoContract]
public class ExceptionResponse : Response
{
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
        ExceptionType = exceptionType;
        CallStack = ex.ToString();
        Message = ex.Message;
    }

    public override ResponseType ResponseType => ResponseType.Exception;

    /// <summary>
    ///     Server side call stack
    /// </summary>
    [field: ProtoMember(1)]
    public string CallStack { get; }

    /// <summary>
    ///     Server side exception message
    /// </summary>
    [field: ProtoMember(2)]
    public string Message { get; }

    [field: ProtoMember(3)] public ExceptionType ExceptionType { get; set; }
}