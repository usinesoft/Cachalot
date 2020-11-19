using System.IO;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Server;
using Server.HostServices;

namespace WindowsService
{
    class CachalotService : ServiceBase
    {
        private ManualResetEvent _stopEvent;
        private HostedService _service;

        protected override void OnStart(string[] args)
        {
            var exeDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            Directory.SetCurrentDirectory(exeDirectory);

            _stopEvent = new ManualResetEvent(false);

            _service = new HostedService(HostServices.Log, _stopEvent);

            // allow for longer startup times
            Task.Factory.StartNew(() => _service.Start(null));

            base.OnStart(args);
        }

        protected override void OnStop()
        {
            _service.Stop();
            base.OnStop();
        }

        
    }
}