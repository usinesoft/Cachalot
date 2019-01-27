using System;
using System.IO;
using Server;
using Topshelf;
using Unity;

namespace Host
{
    class Program
    {
        static void Main()
        {
            // This will ensure that future calls to Directory.GetCurrentDirectory()
            // returns the actual executable directory and not something like C:\Windows\System32 
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);


            const string serviceName = "Cachalot";
            const string displayName = "Cachalot DB Backend Service";
            const string description = "A node in a distributed database";

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                HostServices.HostServices.Container.Resolve<ILog>().LogError($"Unhandled exception: \n {args.ExceptionObject}");
            };

            HostFactory.Run(x =>
            {
                x.Service<HostedService>(sc =>
                {
                    sc.ConstructUsing(name => HostServices.HostServices.Container.Resolve<HostedService>());

                    // the start and stop methods for the service
                    sc.WhenStarted((s, hostControl) => s.Start(hostControl));
                    sc.WhenStopped((s, hostControl) => s.Stop(hostControl));
                });


                x.RunAsLocalSystem();

                x.SetDescription(description);
                x.SetDisplayName(displayName);
                x.SetServiceName(serviceName);
            });
        }
    }
}
