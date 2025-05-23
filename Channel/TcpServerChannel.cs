﻿//#define DEBUG_VERBOSE

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Client;
using Client.ChannelInterface;
using Client.Core;
using Client.Messages;
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed


namespace Channel;

public class TcpServerChannel : IServerChannel
{
    private readonly List<TcpClient> _connectedClients = new();
    private Task _acceptThread;

    CancellationTokenSource _cancellationTokenSource = new();


    private long _connections;
    private TcpListener _listener;

    public event EventHandler<RequestEventArgs> RequestReceived;

    public long Connections => Interlocked.Read(ref _connections);


    public int Init(int port = 0)
    {
        _listener = new(new IPEndPoint(IPAddress.IPv6Any, port))
        {
            Server =
            {
                DualMode = true,
                //ReceiveBufferSize = 1_024_000,
                //SendBufferSize = 1_024_000
            }
        };
        _listener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
        _listener.Server.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);


        _listener.Start();

        if (_listener.LocalEndpoint is not IPEndPoint endpoint)
            throw new NotSupportedException("Can not initialize server");

        return endpoint.Port;
    }

    public void Start()
    {
        _cancellationTokenSource = new CancellationTokenSource();

        if (_listener == null)
            throw new NotSupportedException("Call Init before calling Start");

        
        _acceptThread = Task.Run(()=> WaitForClients(_cancellationTokenSource.Token));
        
    }

    public void Stop()
    {
        try
        {
            _cancellationTokenSource.Cancel();
            
            _acceptThread.Wait(TimeSpan.FromMilliseconds(200));

            
            
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

    private async Task WaitForClients(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                client.Client.NoDelay = true;
                lock (_connectedClients)
                {
                    _connectedClients.Add(client);
                }

                Interlocked.Increment(ref _connections);

                Task.Run(async () => { await ClientLoop(client, ct); }, ct);
            }
            catch(TaskCanceledException)
            {
                Dbg.Trace("stop accepting clients");
            }
            catch (Exception ex)
            {
                Dbg.Trace("error in WaitForClients:" + ex.Message);
                break;
            }
        }
            


        lock (_connectedClients)
        {
            foreach (var connectedClient in _connectedClients) connectedClient.Close();
        }
    }

    private async Task ClientLoop(TcpClient client, CancellationToken ct)
    {
        
        Stream clientStream = client.GetStream();

        Thread.CurrentThread.Name = "client loop (server thread)";

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var buffer = new byte[1];
                var bytesCount = await clientStream.ReadAsync(buffer, 0, 1, ct);

                if (bytesCount == 0) // connection closed
                {
                    Dbg.Trace("0 bytes received:connection closed by client", true);
                    break;
                }


                var inputType = buffer[0];

                Dbg.Trace($"input type {inputType}");


                // if it is a simple ping do not expect a request
                if (inputType == Constants.PingCookie)
                {
                    Dbg.Trace("send ping cookie");
                    clientStream.WriteByte(Constants.PingCookie);
                }
                else if (inputType == Constants.CloseCookie)
                {
                    Dbg.Trace("received close cookie", true);
                    break;
                }
                else
                {
                    Dbg.Trace("received request cookie");

                    var request = await Streamer.FromStreamAsync<Request>(clientStream);

                    Dbg.Trace("request received in client loop");

                    if (RequestReceived != null)
                    {
                        var sc = new ServerClient(client);
                        RequestReceived(this, new(request, sc));
                    }
                    else
                    {
                        break;
                    }


                }
            }
        }
        catch (TaskCanceledException)
        {
            Dbg.Trace("cancelled client loop");
        }
        catch (IOException)
        {
            Dbg.Trace("client disconnected", true);
            //client disconnected (nothing to do)
        }
        // ReSharper disable EmptyGeneralCatchClause
        catch (Exception)
            // ReSharper restore EmptyGeneralCatchClause
        {
            Dbg.Trace("unknown exception while reading client", true);
            //ignore 
        }
        finally
        {
            Dbg.Trace("client disconnected", true);
            lock (_connectedClients)
            {
                _connectedClients.Remove(client);
                Interlocked.Decrement(ref _connections);
            }
            client.Close();

        }
    }


    /// <summary>
    ///     Client as seen from the server
    /// </summary>
    private sealed class ServerClient : IClient
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
            catch (Exception)
            {
                //ignore
            }

            
        }

        public void SendMany(ICollection<JsonDocument> items)
        {
            Stream stream = _tcpClient.GetStream();

            var memStream = new MemoryStream();
            Streamer.ToStreamMany(memStream, items);
            Task.Run(() =>
            {
                memStream.Seek(0, SeekOrigin.Begin);
                stream.Write(memStream.GetBuffer(), 0,
                    (int)memStream.Length);
            });
        }

        public void SendMany(ICollection<PackedObject> items, int[] selectedIndexes, string[] aliases)
        {
            try
            {
                Stream stream = _tcpClient.GetStream();

                switch (items.Count)
                {
                    // fastest processing for 0 or 1 items
                    case 0:
                        Streamer.OneToStream(stream, null, selectedIndexes, aliases);
                        break;
                    case 1:
                        Streamer.OneToStream(stream, items.First(), selectedIndexes, aliases);
                        break;
                    // if less items than a threshold serialize into a buffer so the read-lock can be released immediately
                    default:
                    {
                        if (items.Count < Constants.StreamingThreshold)
                        {
                            var memStream = new MemoryStream();
                            Streamer.ToStreamMany(memStream, items, selectedIndexes, aliases);
                            Task.Run(() =>
                            {
                                memStream.Seek(0, SeekOrigin.Begin);
                                stream.Write(memStream.GetBuffer(), 0,
                                    (int)memStream.Length);
                            });
                        }
                        else
                        {
                            Streamer.ToStreamMany(stream, items, selectedIndexes, aliases);
                        }

                        break;
                    }
                }
            }
            
            catch (Exception)
            {
                //ignore
            }
        }

        #endregion
    }
}