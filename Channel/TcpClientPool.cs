//#define DEBUG_VERBOSE
#region

using System;
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

            client.Connect(_address, _port);


            Dbg.Trace("end get shiny new resource");

            if (!client.Connected)
                return null;

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

    protected override bool IsStillValid(TcpClient tcp)
    {
        if (tcp == null)
            return false;


        try
        {
            if (!tcp.Connected)
            {
                Dbg.Trace("Connection not valid anymore", true);
                return false;
            }
                
            
            Dbg.Trace("sending ping request");

            int pingAnswer = 0;

            try
            {
                var stream = tcp.GetStream();
                stream.WriteByte(Constants.PingCookie); // ping 
                stream.Flush();
                pingAnswer = stream.ReadByte();
            }
            catch (Exception )
            {
                Console.WriteLine();
                throw;
            }

            // this should never happen. 
            if (pingAnswer != Constants.PingCookie)
            {
                Dbg.Trace("invalid ping answer", true);
            }

            return pingAnswer == Constants.PingCookie;
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
            var stream = resource.GetStream();
            stream.WriteByte(Constants.CloseCookie);
            stream.Flush();

            stream.Close();
            resource.Close();
        }
    }
}