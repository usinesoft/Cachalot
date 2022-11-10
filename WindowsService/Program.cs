using System;
using System.ServiceProcess;

namespace WindowsService
{
    class Program
    {
        static void Main(string[] args)
        {
            if (!OperatingSystem.IsWindows())
                throw new NotSupportedException("The service works only on Windows");

            ServiceBase.Run(new CachalotService());
            
        }
    }
}
