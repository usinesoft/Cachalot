using System;
using System.ServiceProcess;

namespace WindowsService;

internal class Program
{
    private static void Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
            throw new NotSupportedException("The service works only on Windows");

        ServiceBase.Run(new CachalotService());
    }
}