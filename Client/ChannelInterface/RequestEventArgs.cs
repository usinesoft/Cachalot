using System;

namespace Client.ChannelInterface;

/// <summary>
///     All data needed by the server to respond to a client request
///     Contains an abstract request and an interface used to send responses to the client
/// </summary>
public class RequestEventArgs : EventArgs
{
    /// <summary>
    /// </summary>
    /// <param name="request"></param>
    /// <param name="client"></param>
    public RequestEventArgs(Request request, IClient client)
    {
        Request = request;
        Client = client;
    }

    /// <summary>
    ///     Request received from the client
    /// </summary>
    public Request Request { get; set; }

    /// <summary>
    ///     The channel interface needed to send response data to the client
    /// </summary>
    public IClient Client { get; set; }
}