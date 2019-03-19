#region

using System;
using System.Net;
using System.Net.Sockets;

#endregion

namespace Channel
{
    public class TcpClientPool : PoolStrategy<TcpClient>
    {
        
        private readonly int _port;

        private readonly IPAddress _address;

        public TcpClientPool(int poolCapacity, int preloaded, string host, int port) : base(poolCapacity)
        {
            
            _port = port;

            // accept hostname, IPV4 or IPV6 address
          
            if (!IPAddress.TryParse(host, out _address))
                _address = Dns.GetHostEntry(host).AddressList[0];

            _address = _address.MapToIPv6();

            Preload(preloaded);
        }


        protected override TcpClient GetShinyNewResource()
        {
            try
            {
                var client = new TcpClient(AddressFamily.InterNetworkV6) {Client = {DualMode = true}, NoDelay = true};

                client.Connect (_address, _port);
                

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
                stream.WriteByte(Consts.PingCookie); // ping 
                var pingAnswer = stream.ReadByte();

                // this should never happen. 
                if (pingAnswer != Consts.PingCookie)
                {
                    throw new NotSupportedException("Wrong answer to ping request");
                }

                return pingAnswer == Consts.PingCookie;

            }
            catch (Exception )
            {
                return false;
            }
            
        }

        protected override void Release(TcpClient resource)
        {
            resource.Client.Close();
            resource.Close();
        }
    }
}