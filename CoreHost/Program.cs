using System.Threading;
using Server;
using Server.HostServices;

namespace Host
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            string instance = null;
            // an instance-name can be specified
            // 
            if (args.Length == 1) instance = args[0].Trim().ToLower();

            var stopEvent = new ManualResetEvent(false);

            var service = new HostedService(HostServices.Log, stopEvent);

            service.Start(instance);

            stopEvent.WaitOne();
        }
    }
}