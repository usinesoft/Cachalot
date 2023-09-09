using System;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Server;
using Server.HostServices;

namespace WindowsService;

internal class CachalotService : ServiceBase
{
    private HostedService _service;
    private ManualResetEvent _stopEvent;

    protected override void OnStart(string[] args)
    {
        if (!OperatingSystem.IsWindows())
            throw new NotSupportedException("The service works only on Windows");

        base.OnStart(args);

        var exeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        Directory.SetCurrentDirectory(exeDirectory);

        _stopEvent = new(false);

        _service = new(HostServices.Log, _stopEvent);


        // allow for longer startup times
        Task.Factory.StartNew(() => _service.Start(args.Length > 0 ? args[0] : null));
    }

    protected override void OnStop()
    {
        if (!OperatingSystem.IsWindows())
            throw new NotSupportedException("The service works only on Windows");


        _service.Stop();
        base.OnStop();
    }
}