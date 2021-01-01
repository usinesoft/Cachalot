using System.Collections.Generic;
using Client.Core;

namespace Client.ChannelInterface
{
    /// <summary>
    ///     Abstract view of the client as seen from the server
    ///     Allows to send responses to the client
    /// </summary>
    public interface IClient
    {
        /// <summary>
        ///     Send a generic response to the client. The response is serialized before being sent
        /// </summary>
        /// <param name="response"></param>
        void SendResponse(Response response);

        /// <summary>
        ///     Send a collection of cached object. No serialization is required on the server side
        /// </summary>
        /// <param name="items"></param>
        void SendMany(ICollection<PackedObject> items);

        
        /// <summary>
        ///     Wait for client confirmation (used in two-stage transactions).
        ///     If null has been returned the client did not answer in the limited amount of time
        /// </summary>
        /// <returns></returns>
        bool? ShouldContinue();

    }
}