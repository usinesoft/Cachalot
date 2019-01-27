using System;

namespace Client.ChannelInterface
{
    /// <summary>
    ///     Server side of a channel.
    /// </summary>
    public interface IServerChannel
    {
        long Connections { get; }

        /// <summary>
        ///     Raised on the server site when a client sends a request
        /// </summary>
        event EventHandler<RequestEventArgs> RequestReceived;
    }
}