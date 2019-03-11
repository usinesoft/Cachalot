using System.Threading;

namespace CoreHost
{
    class Program
    {
        static void Main(string[] args)
        {

            string instance = null;
            // an instance-name can be specified
            // 
            if (args.Length == 1)
            {
                instance = args[0].Trim().ToLower();
            }

            var stopEvent = new ManualResetEvent(false);
            
            var service = new HostedService(HostServices.HostServices.Log, stopEvent);
            
            service.Start(instance);

            stopEvent.WaitOne();

        }
    }
}
