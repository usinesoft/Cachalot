using Client.ChannelInterface;

namespace Client.Core;

/// <summary>
///     A handler called when an exception is raised on the server, during a streaming operation
/// </summary>
/// <param name="exception"></param>
public delegate void ExceptionHandler(ExceptionResponse exception);