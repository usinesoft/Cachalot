using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Client.ChannelInterface;
using Client.Core;
using Client.Interface;
using Client.Messages;
using JetBrains.Annotations;
using ProtoBuf;

namespace Channel
{
    public class TcpClientChannel : IClientChannel
    {
        private readonly TcpClientPool _connectionPool;


        readonly Dictionary<Guid, TcpClient> _connectionBySession = new Dictionary<Guid, TcpClient>();

        public void ReserveConnection(Guid sessionId)
        {
            
            var connection = _connectionPool.Get();
            if (connection == null)
            {
                throw new CacheException("Not connected to server");
            }

            lock (_connectionBySession)
            {
                _connectionBySession.Add(sessionId, connection);
            }

        }

        public void ReleaseConnection(Guid sessionId)
        {
            lock (_connectionBySession)
            {
                _connectionPool.Put(_connectionBySession[sessionId]);
                _connectionBySession.Remove(sessionId);
            }
        }

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
            public TcpSession([NotNull] TcpClient client)
            {
                Client = client ?? throw new ArgumentNullException(nameof(client));
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

        public IEnumerable<RankedItem> SendStreamRequest(Request request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            Guid sessionId = default;
            if (request is IHasSession hasSession)
            {
                sessionId = hasSession.SessionId;
            }

            var connection = InternalGetConnection(sessionId);


            if (connection == null || connection.Connected == false)
                throw new CacheException("Not connected to server");

            var stream = connection.GetStream();
            try
            {
                stream.WriteByte(Constants.RequestCookie);
                Streamer.ToStream(stream, request);


                var enumerable = Streamer.EnumerableFromStream(stream);
                foreach (var item in enumerable)
                    yield return item;
            }
            finally
            {
                Streamer.SendAck(stream);

                if (sessionId == default)
                {
                    _connectionPool.Put(connection);
                }
                
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

            stream.WriteByte(Constants.RequestCookie);

            Streamer.ToStream(stream, request);
        }

        private Response SendRequest(Session session, Request request)
        {
            if (!(session is TcpSession tcpSession))
                throw new ArgumentException("Invalid session type", nameof(session));

            var client = tcpSession.Client;

            if (client == null || client.Connected == false)
                throw new CacheException("Not connected to server");
            
            var stream = client.GetStream();

            stream.WriteByte(Constants.RequestCookie);

            Streamer.ToStream(stream, request);

            var response = Streamer.FromStream<Response>(stream);

            
            return response;
        }

        //TODO investigate async
        //private async Response SendRequestAsync(Session session, Request request)
        //{
        //    if (!(session is TcpSession tcpSession))
        //        throw new ArgumentException("Invalid session type", nameof(session));

        //    var client = tcpSession.Client;

        //    if (client == null || client.Connected == false)
        //        throw new CacheException("Not connected to server");
            
        //    var stream = client.GetStream();

        //    await stream.WriteAsync(new byte[] {Constants.RequestCookie});
            

        //    Streamer.ToStream(stream, request);

        //    var response = Streamer.FromStream<Response>(stream);

            
        //    return response;
        //}

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


        /// <summary>
        /// Use a reserved connection if available (inside a consistent read operation), otherwise get a new one
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        TcpClient InternalGetConnection(Guid sessionId)
        {
            TcpClient connection = null;

            
            lock (_connectionBySession)
            {
                _connectionBySession.TryGetValue(sessionId, out connection);
            }

            // otherwise get a new one
            connection ??= _connectionPool.Get();

            return connection;

        }

        public Response SendRequest(Request request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));


            Guid sessionId = default;
            if (request is IHasSession hasSession)
            {
                sessionId = hasSession.SessionId;
            }

            var connection = InternalGetConnection(sessionId);
            
            try
            {
                if (connection == null || connection.Connected == false)
                    throw new CacheException("Not connected to server");

                var stream = connection.GetStream();

                stream.WriteByte(Constants.RequestCookie);

                Streamer.ToStream(stream, request);

                var response = Streamer.FromStream<Response>(stream);

                Streamer.SendAck(stream);

                if (response == null)
                    throw new CacheException("Error : invalid response from server");


                return response;
            }
            finally
            {
                if (sessionId == default && connection != null) // not in a session so return the connection to the pool
                {
                    _connectionPool.Put(connection);
                }
                
            }
        }


        #endregion
    }
}