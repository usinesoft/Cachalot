using System;
using System.Collections.Generic;
using Client.Core;

namespace Client.ChannelInterface;

/// <summary>
///     Client side of a synchronous communication channel
/// </summary>
public interface IClientChannel : IDisposable
{
    /// <summary>
    ///     Synchronous generic call
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    Response SendRequest(Request request);


    /// <summary>
    ///     Get a connection from the pool and keep it for multiple usages
    /// </summary>
    /// <param name="sessionId"></param>
    void ReserveConnection(Guid sessionId);

    /// <summary>
    ///     Release the reserved connection
    /// </summary>
    void ReleaseConnection(Guid sessionId);


    IEnumerable<RankedItem> SendStreamRequest(Request request);
    IEnumerable<RankedItem2> SendStreamRequest2(Request request);

    //Start a complex communication session
    Session BeginSession();

    //End a complex communication session
    void EndSession(Session session);

    /// <summary>
    ///     Send a request to the communication channel. Do not wait for response.
    ///     Used in more complex communication scenarios where different types of data are received
    /// </summary>
    /// <param name="session"></param>
    /// <param name="request"></param>
    void PushRequest(Session session, Request request);


    /// <summary>
    ///     In a session we can receive more than one answer for a request
    /// </summary>
    /// <param name="session"></param>
    /// <returns></returns>
    Response GetResponse(Session session);

    /// <summary>
    ///     Send a continue or stop request. Used for two stage transactions
    /// </summary>
    /// <param name="session"></param>
    /// <param name="shouldContinue"></param>
    bool Continue(Session session, bool shouldContinue);
}