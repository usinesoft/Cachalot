#region

using Client.Core;
using System;
using System.Net;
using System.Net.Sockets;

#endregion

namespace Channel
{
    public class TcpClientPool : PoolStrategy<TcpClient>
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
                var client = new TcpClient(AddressFamily.InterNetworkV6) { Client = { DualMode = true }, NoDelay = true };

                client.Connect(_address, _port);

                //var connectDone = new ManualResetEvent (false);

                //var endConnect = new AsyncCallback (o => {

                //    try
                //    {
                //        var state = (TcpClient) o.AsyncState;
                //        state.EndConnect(o);
                //    }
                //    catch (Exception)
                //    {
                //        // ignore
                //    }
                //    finally
                //    {
                //        connectDone.Set ();    
                //    }

                //});


                //client.BeginConnect(_address, _port, endConnect, client);
                //connectDone.WaitOne (TimeSpan.FromMilliseconds(Constants.ConnectionTimeoutInMilliseconds));


                if (!client.Connected)
                    return null;

                return client;
            }
            catch (Exception)
            {
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
                var stream = tcp.GetStream();
                stream.WriteByte(Constants.PingCookie); // ping 
                var pingAnswer = stream.ReadByte();

                // this should never happen. 
                if (pingAnswer != Constants.PingCookie) throw new NotSupportedException("Wrong answer to ping request");

                return pingAnswer == Constants.PingCookie;
            }
            catch (Exception)
            {
                return false;
            }
        }

        protected override void Release(TcpClient resource)
        {
            if (resource != null)
            {
                resource.Client.Close();
                resource.Close();
            }
            
        }
    }
}