//#define DEBUG_VERBOSE
#region

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Client;
using Client.Core;

#endregion


namespace Channel;

public class
    TcpClientPool : PoolStrategy<TcpClient>
{
    private readonly IPAddress _address;

    private readonly int _port;

    public TcpClientPool(int poolCapacity, int preloaded, string host, int port) : base(poolCapacity)
    {
        _port = port;

        // accept hostname, IPV4 or IPV6 address

        if (!IPAddress.TryParse(host, out _address))
            _address = Dns.GetHostEntry(host).AddressList[0];

        _address = _address.MapToIPv6();

        PreLoad(preloaded);
    }


    protected override TcpClient GetShinyNewResource()
    {
        try
        {
            Dbg.Trace("begin get shiny new resource");

            var client = new TcpClient(AddressFamily.InterNetworkV6) { Client = { DualMode = true }, NoDelay = true };

            try
            {
                client.Connect(_address, _port);
            }
            catch (Exception )
            {
                Dbg.Trace("can not connect to server");
                return null;
            }


            Dbg.Trace("end get shiny new resource");

            
            return client;

            
        }
        catch (Exception)
        {
            Dbg.Trace("exception while getting new connection", true);
            //by returning null we notify the pool that the external connection
            //provider is not available any more
            return null;
        }
    }

    private readonly Dictionary<TcpClient, DateTime> _lastTimeCheckedByConnection = new();

    public static readonly TimeSpan CheckPeriod = TimeSpan.FromSeconds(10);

    /// <summary>
    /// To avoid pinging the server each time we get a connection from the pool
    /// </summary>
    /// <param name="client"></param>
    /// <returns></returns>
    bool WasCheckedRecently(TcpClient client)
    {
        if (_lastTimeCheckedByConnection.TryGetValue(client, out var time) && (DateTime.Now - time)  < CheckPeriod)
        {
            return true;
        }

        return false;
    }

    protected override bool IsStillValid(TcpClient tcp)
    {
        if (tcp == null)
            return false;

        if (WasCheckedRecently(tcp))
            return true;

        try
        {
            if (!tcp.Connected)
            {
                Dbg.Trace("Connection not valid anymore", true);
                return false;
            }
                
            
            Dbg.Trace("sending ping request");

            int pingAnswer = 0;

            var stream = tcp.GetStream();
            stream.WriteByte(Constants.PingCookie); // ping 
            stream.Flush();
            pingAnswer = stream.ReadByte();
        
            // this should never happen. 
            if (pingAnswer != Constants.PingCookie)
            {
                Dbg.Trace("invalid ping answer", true);
            }

            bool isValid = pingAnswer == Constants.PingCookie;

            if (isValid)
            {
                _lastTimeCheckedByConnection[tcp] = DateTime.Now;
            }

            return isValid;
        }
        catch (Exception)
        {
            return false;
        }
    }

    protected override void Release(TcpClient resource)
    {
        Dbg.Trace("Close connection");

        if (resource != null)
        {
            Dbg.Trace("Release: closing connection");
            
            // proactive close request
            try
            {
                var stream = resource.GetStream();
                stream.WriteByte(Constants.CloseCookie);
                stream.Flush();

                stream.Close();
                resource.Close();
            
                _lastTimeCheckedByConnection.Remove(resource);
            }
            catch (Exception )
            {
                // ignore (the server is down, no need to release)
            }
        }
    }
}