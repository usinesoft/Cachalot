#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Client.ChannelInterface;
using Client.Core;

#endregion

namespace Channel
{
    /// <summary>
    ///     In process channel (for tests only)
    /// </summary>
    public class InProcessChannel : IClientChannel, IServerChannel
    {
        #region IDisposable Members

        public void Dispose()
        {
            //nothing to dispose
        }

        #endregion

        #region IServerChannel Members

        public long Connections => 1;

        #endregion

        #region IServerChannel Members

        public event EventHandler<RequestEventArgs> RequestReceived;

        #endregion

        private class InProcessSession : Session
        {
            public InProcessSession(ClientData clientData)
            {
                ClientData = clientData;
            }

            public ClientData ClientData { get; }
        }

        private class ClientData : IClient
        {
            private readonly ManualResetEvent _dataReceived;
            private readonly ManualResetEvent _clientResponseReceived;
            private readonly MemoryStream _stream;

            private bool? _shouldContinue;

            public ClientData()
            {
                _dataReceived = new ManualResetEvent(false);
                _clientResponseReceived = new ManualResetEvent(false);
                _stream = new MemoryStream();
            }

            public void SendResponse(Response response)
            {
                _stream.SetLength(0);
                Streamer.ToStream(_stream, response);
                _stream.Seek(0, SeekOrigin.Begin);
                _dataReceived.Set();
            }

            public void SendMany(ICollection<CachedObject> items)
            {
                Streamer.ToStreamMany(_stream, items);
                _stream.Seek(0, SeekOrigin.Begin);
                _dataReceived.Set();
            }

            

            public void SendManyGeneric<TItemType>(ICollection<TItemType> items) where TItemType : class
            {
                
                Streamer.ToStreamGeneric(_stream, items);
                _stream.Seek(0, SeekOrigin.Begin);
                _dataReceived.Set();
            }

            

            public void SendMany<THeader>(THeader header, ICollection<CachedObject> items) where THeader : class
            {
                Streamer.ToStream(_stream, header, items);
                _stream.Seek(0, SeekOrigin.Begin);
                _dataReceived.Set();
            }


            public bool? ShouldContinue()
            {
                _shouldContinue = null; // reset

                SendResponse(new ReadyResponse());

                bool answerReceived = _clientResponseReceived.WaitOne(Consts.ClientTimeoutInMilliseconds);

                if (!answerReceived)
                {
                    return null;
                }

                Debug.Assert(_shouldContinue != null, nameof(_shouldContinue) + " != null");
                return _shouldContinue.Value;
            }

            public void WaitForAck()
            {
                
            }

            #region methods called on the client side

            public Stream WaitForData()
            {
                _dataReceived.WaitOne();
                return _stream;
            }

            public void Continue(bool ok)
            {
                _shouldContinue = ok;
                _clientResponseReceived.Set();

            }

            #endregion
        }


        #region IClientChannel Members

        public Response SendRequest(Request request)
        {
            var rcvRequest = request;

            Response response = null;


            if (RequestReceived != null)
            {
                var client = new ClientData();
                RequestReceived(this, new RequestEventArgs(rcvRequest, client));
                var stream = client.WaitForData();

                response = Streamer.FromStream<Response>(stream);
            }

            if (response == null)
                return new NullResponse();

            return response;
        }

      


        public void SendStreamRequest<TItemType>(Request request, DataHandler<TItemType> dataHandler,
            ExceptionHandler exceptionHandler)
        {
            if (RequestReceived != null)
            {
                var client = new ClientData();
                RequestReceived(this, new RequestEventArgs(request, client));
                var stream = client.WaitForData();
                Streamer.FromStream(stream, dataHandler, exceptionHandler);
            }
        }

        public IEnumerable<TItemType> SendStreamRequest<TItemType>(Request request)
        {
            if (RequestReceived != null)
            {
                var client = new ClientData();
                RequestReceived(this, new RequestEventArgs(request, client));
                var stream = client.WaitForData();
                return Streamer.EnumerableFromStream<TItemType>(stream);
            }

            // otherwise return empty collection
            return new List<TItemType>();
        }


        public Session BeginSession()
        {
            var client = new ClientData();
            return new InProcessSession(client);
        }

        public void EndSession(Session session)
        {
            //nothing to do
        }

        public void PushRequest(Session session, Request request)
        {
            if (!(session is InProcessSession inProcessSession))
                throw new ArgumentException("Invalid session type", nameof(session));

            var client = inProcessSession.ClientData;
            RequestReceived?.Invoke(this, new RequestEventArgs(request, client));
        }

        public Response SendRequest(Session session, Request request)
        {
            if (!(session is InProcessSession inProcessSession))
                throw new ArgumentException("Invalid session type", nameof(session));

            var client = inProcessSession.ClientData;
            var rcvRequest = request;

            Response response = null;


            if (RequestReceived != null)
            {
                
                RequestReceived(this, new RequestEventArgs(rcvRequest, client));
                var stream = client.WaitForData();

                response = Streamer.FromStream<Response>(stream);
            }

            if (response == null)
                return new NullResponse();

            return response;
        }

        public Response GetResponse(Session session)
        {
            if (!(session is InProcessSession inProcessSession))
                throw new ArgumentException("Invalid session type", nameof(session));

            var client = inProcessSession.ClientData;



            var stream = client.WaitForData();

            var response = Streamer.FromStream<Response>(stream);

            if (response == null)
                return new NullResponse();

            return response;
        }

        public void GetStreamedCollection<TItemType>(Session session, DataHandler<TItemType> dataHandler,
            ExceptionHandler exceptionHandler)
        {
            if (!(session is InProcessSession inProcessSession))
                throw new ArgumentException("Invalid session type", nameof(session));

            var client = inProcessSession.ClientData;
            var stream = client.WaitForData();
            Streamer.FromStream(stream, dataHandler, exceptionHandler);
        }


        public TItem GetOneObject<TItem>(Session session)
        {
            if (!(session is InProcessSession inProcessSession))
                throw new ArgumentException("Invalid session type", nameof(session));

            var client = inProcessSession.ClientData;
            var stream = client.WaitForData();
            return Streamer.FromStream<TItem>(stream);
        }

        public bool Continue(Session session, bool ok)
        {
            if (!(session is InProcessSession inProcessSession))
                throw new ArgumentException("Invalid session type", nameof(session));

            inProcessSession.ClientData.Continue(ok);

            // the return value is used only for two stage transaction (never used in an in-process server)
            return true;
        }

        #endregion
    }
}