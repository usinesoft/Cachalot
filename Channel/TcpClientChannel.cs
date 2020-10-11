using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Client.ChannelInterface;
using Client.Core;
using Client.Interface;
using Client.Messages;
using ProtoBuf;

namespace Channel
{
    public class TcpClientChannel : IClientChannel
    {
        private readonly TcpClientPool _connectionPool;

        /// <summary>
        ///     Use the static constructor to prepare the serializers thus preventing a race condition when lazy-initializing in
        ///     multi threaded environment
        /// </summary>
        static TcpClientChannel()
        {
            Serializer.PrepareSerializer<Request>();
        }

        public TcpClientChannel(TcpClientPool connectionPool)
        {
            _connectionPool = connectionPool ?? throw new ArgumentNullException(nameof(connectionPool));
        }

        private class TcpSession : Session
        {
            public TcpSession(TcpClient client)
            {
                Client = client;
            }

            public TcpClient Client { get; }
        }

        #region IDisposable Members

        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                _connectionPool?.Dispose();

                _disposed = true;
            }
        }

        #endregion

        #region IClientChannel Members

        public IEnumerable<RankedItem> SendStreamRequest<TItemType>(Request request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var client = _connectionPool.Get();

            if (client == null || client.Connected == false)
                throw new CacheException("Not connected to server");

            var stream = client.GetStream();
            try
            {
                stream.WriteByte(Consts.RequestCookie);
                Streamer.ToStream(stream, request);


                var enumerable = Streamer.EnumerableFromStream<TItemType>(stream);
                foreach (var item in enumerable)
                    yield return item;
            }
            finally
            {
                Streamer.SendAck(stream);
                _connectionPool.Put(client);
            }
        }

        public Session BeginSession()
        {
            var client = _connectionPool.Get();
            if (client == null || client.Connected == false)
                throw new CacheException("Not connected to server");

            return new TcpSession(client);
        }

        public void EndSession(Session session)
        {
            if (!(session is TcpSession tcpSession))
                throw new ArgumentException("Invalid session type", nameof(session));

            _connectionPool.Put(tcpSession.Client);
        }

        public void PushRequest(Session session, Request request)
        {
            if (!(session is TcpSession tcpSession))
                throw new ArgumentException("Invalid session type", nameof(session));

            var client = tcpSession.Client;

            if (client == null || client.Connected == false)
                throw new CacheException("Not connected to server");
            var stream = client.GetStream();

            stream.WriteByte(Consts.RequestCookie);

            Streamer.ToStream(stream, request);
        }

        public Response SendRequest(Session session, Request request)
        {
            if (!(session is TcpSession tcpSession))
                throw new ArgumentException("Invalid session type", nameof(session));

            var client = tcpSession.Client;

            if (client == null || client.Connected == false)
                throw new CacheException("Not connected to server");
            
            var stream = client.GetStream();

            stream.WriteByte(Consts.RequestCookie);

            Streamer.ToStream(stream, request);

            var response = Streamer.FromStream<Response>(stream);

            //Streamer.SendAck(tcpSession.Client.GetStream());

            return response;
        }

        public Response GetResponse(Session session)
        {
            if (!(session is TcpSession tcpSession))
                throw new ArgumentException("Invalid session type", nameof(session));

            var client = tcpSession.Client;

            if (client == null || client.Connected == false)
                throw new CacheException("Not connected to server");
            var stream = client.GetStream();


            var response = Streamer.FromStream<Response>(stream);


            return response;
        }

        public bool Continue(Session session, bool shouldContinue)
        {
            var response = SendRequest(session, new ContinueRequest {Rollback = !shouldContinue});


            return response is ReadyResponse;
        }

        public void GetStreamedCollection<TItemType>(Session session, DataHandler<TItemType> dataHandler,
            ExceptionHandler exceptionHandler)
        {
            if (!(session is TcpSession tcpSession))
                throw new ArgumentException("Invalid session type", nameof(session));

            var client = tcpSession.Client;

            if (client == null || client.Connected == false)
                throw new CacheException("Not connected to server");

            var stream = client.GetStream();

            Streamer.FromStream(stream, dataHandler, exceptionHandler);
        }


        public TItem GetOneObject<TItem>(Session session)
        {
            if (!(session is TcpSession tcpSession))
                throw new ArgumentException("Invalid session type", nameof(session));

            var client = tcpSession.Client;

            if (client == null || client.Connected == false)
                throw new CacheException("Not connected to server");

            var stream = client.GetStream();

            return Streamer.FromStream<TItem>(stream);
        }

        public Response SendRequest(Request request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));


            var client = _connectionPool.Get();

            if (client == null || client.Connected == false)
                throw new CacheException("Not connected to server");

            var stream = client.GetStream();

            stream.WriteByte(Consts.RequestCookie);

            Streamer.ToStream(stream, request);

            var response = Streamer.FromStream<Response>(stream);

            Streamer.SendAck(stream);

            _connectionPool.Put(client);

            if (response == null)
                throw new CacheException("Error : invalid response from server");


            return response;
        }


        public void SendStreamRequest<TItemType>(Request request, DataHandler<TItemType> dataHandler,
            ExceptionHandler exceptionHandler)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var client = _connectionPool.Get();

            if (client == null || client.Connected == false)
                throw new CacheException("Not connected to server");

            try
            {
                var stream = client.GetStream();

                stream.WriteByte(Consts.RequestCookie);

                Streamer.ToStream(stream, request);


                Streamer.FromStream(stream, dataHandler, exceptionHandler);

                Streamer.SendAck(stream);
            }
            catch (Exception ex)
            {
                throw new CacheException(ex.Message);
            }
            finally
            {
                _connectionPool.Put(client);
            }
        }

        #endregion
    }
}