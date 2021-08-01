//#define DEBUG_VERBOSE
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Client;
using Client.ChannelInterface;
using Client.Core;
using Client.Messages;

namespace Channel
{
    public class TcpServerChannel : IServerChannel
    {
        private readonly List<TcpClient> _connectedClients = new List<TcpClient>();
        private Thread _acceptThread;


        private long _connections;
        private TcpListener _listener;

        public event EventHandler<RequestEventArgs> RequestReceived;

        public long Connections => Interlocked.Read(ref _connections);


        public int Init(int port = 0)
        {
            _listener = new TcpListener(new IPEndPoint(IPAddress.IPv6Any, port));
            _listener.Server.DualMode = true;
            _listener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            _listener.Server.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

            _listener.Start();

            if (!(_listener.LocalEndpoint is IPEndPoint endpoint))
                throw new NotSupportedException("Can not initialize server");

            return endpoint.Port;
        }

        public void Start()
        {
            if (_listener == null)
                throw new NotSupportedException("Call Init before calling Start");


            _acceptThread = new Thread(WaitForClients);
            _acceptThread.Start();
        }

        public void Stop()
        {
            try
            {
                Dbg.Trace("before _listener.Server.Disconnect();");
                _listener.Server.Disconnect(false);
                Dbg.Trace("after _listener.Server.Disconnect();");
                
                
                
                //_listener.Stop();
            }
            catch (Exception)
            {
                // ignore
            }
            finally
            {
                Dbg.Trace("before _listener.Server.Close();");
                _listener.Server.Close();
                Dbg.Trace("after _listener.Server.Close();");
            }
        }

        private void WaitForClients()
        {
            while (true)
                try
                {
                    var client = _listener.AcceptTcpClient();
                    client.Client.NoDelay = true;
                    lock (_connectedClients)
                    {
                        _connectedClients.Add(client);
                    }

                    Interlocked.Increment(ref _connections);
                    var clientThread = new Thread(ClientLoop);
                    clientThread.Start(client);
                }
                catch (Exception)
                {
                    break;
                }


            lock (_connectedClients)
            {
                foreach (var connectedClient in _connectedClients) connectedClient.Close();
            }
        }

        private void ClientLoop(object state)
        {
            var client = (TcpClient) state;

            Stream clientStream = client.GetStream();

            Thread.CurrentThread.Name = "client loop (server thread)";

            try
            {
                while (client.Connected)
                {
                    var inputType = clientStream.ReadByte();

                    if (inputType == -1) // connection closed
                        break;

                    // if its a simple ping do not expect a request
                    if (inputType == Constants.PingCookie)
                    {
                        clientStream.WriteByte(Constants.PingCookie);
                    }
                    else
                    {
                        var request = Streamer.FromStream<Request>(clientStream);


                        if (RequestReceived != null)
                        {
                            var sc = new ServerClient(client);
                            RequestReceived(this, new RequestEventArgs(request, sc));
                        }
                        else
                        {
                            break;
                        }

                        if (request.IsSimple)
                        {
                            // ReSharper disable once RedundantAssignment
                            var ackOkay = Streamer.ReadAck(clientStream);
                            Debug.Assert(ackOkay);
                        }
                    }
                }
            }
            catch (IOException)
            {
                //client disconnected (nothing to do)
            }
// ReSharper disable EmptyGeneralCatchClause
            catch (Exception)
// ReSharper restore EmptyGeneralCatchClause
            {
                //ignore 
            }
            finally
            {
                lock (_connectedClients)
                {
                    _connectedClients.Remove(client);
                    Interlocked.Decrement(ref _connections);
                }
            }
        }


        /// <summary>
        ///     Client as seen from the server
        /// </summary>
        private class ServerClient : IClient
        {
            private readonly TcpClient _tcpClient;

            public ServerClient(TcpClient tcpClient)
            {
                _tcpClient = tcpClient;
            }

            #region IClient Members

            public bool? ShouldContinue()
            {
                try
                {
                    SendResponse(new ReadyResponse());

                    

#if !DEBUG
                    _tcpClient.ReceiveTimeout = Constants.ReceiveTimeoutInMilliseconds;
#endif

                    Stream stream = _tcpClient.GetStream();

                    var cookie = stream.ReadByte();
                    if (cookie != Constants.RequestCookie) return null;

                    var answer = Streamer.FromStream<Request>(stream);

                    if (answer is ContinueRequest @continue)
                    {
                        if (!@continue.Rollback) return true;

                        if (@continue.Rollback) return false;
                    }


                    return null;
                }
                catch (Exception)
                {
                    return null;
                }
            }

            public void SendResponse(Response response)
            {
                try
                {
                    Stream stream = _tcpClient.GetStream();

                    Streamer.ToStream(stream, response);
                }
// ReSharper disable EmptyGeneralCatchClause
                catch (Exception)
// ReSharper restore EmptyGeneralCatchClause
                {
                }

                //_dataReceived.Set();
            }


            public void SendMany(ICollection<PackedObject> items, int[] selectedIndexes, string[] aliases)
            {
                try
                {
                    Stream stream = _tcpClient.GetStream();

                    // if less items than a threshold serialize into a buffer so the read-lock can be released immediately
                    if (items.Count < Constants.StreamingThreshold)
                    {
                        var memStream = new MemoryStream();
                        Streamer.ToStreamMany(memStream, items, selectedIndexes, aliases);
                        ThreadPool.QueueUserWorkItem(delegate(object state)
                        {
                            var networkStream = (Stream) state;
                            memStream.Seek(0, SeekOrigin.Begin);
                            networkStream.Write(memStream.GetBuffer(), 0,
                                (int) memStream.Length);
                        }, stream);
                    }
                    else
                    {
                        Streamer.ToStreamMany(stream, items, selectedIndexes, aliases);
                    }
                }
// ReSharper disable EmptyGeneralCatchClause
                catch (Exception)
// ReSharper restore EmptyGeneralCatchClause
                {
                }
            }


            #endregion
        }
    }
}