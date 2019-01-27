using System;
using System.Collections.Generic;
using Client.Core;

namespace Client.ChannelInterface
{
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
        ///     Get a (potentially big) collection of objects from the server.
        ///     All objects are deserialized to <see cref="TItemType" /> instances.
        /// </summary>
        /// <typeparam name="TItemType">The concrete type of objects (needs to be known only by the client)</typeparam>
        /// <param name="request">a generic request (usually a GetMany())</param>
        /// <param name="dataHandler">delegate to be called for each received item (called in the caller thread)</param>
        /// <param name="exceptionHandler">delegate to be called if the server returns an exception(called in the caller thread)</param>
        void SendStreamRequest<TItemType>(Request request, DataHandler<TItemType> dataHandler,
            ExceptionHandler exceptionHandler);


        IEnumerable<TItemType> SendStreamRequest<TItemType>(Request request);

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


        Response SendRequest(Session session, Request request);


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


        /// <summary>
        ///     Read a typed collection of objects from the underlying communication channel
        /// </summary>
        /// <typeparam name="TItemType"></typeparam>
        /// <param name="session"></param>
        /// <param name="dataHandler">called when an object is received</param>
        /// <param name="exceptionHandler">called when an exception occurs</param>
        void GetStreamedCollection<TItemType>(Session session, DataHandler<TItemType> dataHandler,
            ExceptionHandler exceptionHandler);


        /// <summary>
        ///     Read a single object inside a session
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        TItem GetOneObject<TItem>(Session session);
    }
}