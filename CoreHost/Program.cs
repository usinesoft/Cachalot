using System;
using System.Threading;
using Host;
using Host.HostServices;
using Host.HostServices.Logger;

namespace CoreHost
{
    class Program
    {
        static void Main(string[] args)
        {
           
            var stopEvent = new ManualResetEvent(false);
            
            var service = new HostedService(HostServices.Log, stopEvent);
            
            service.Start();

            stopEvent.WaitOne();

        }
    }
}
