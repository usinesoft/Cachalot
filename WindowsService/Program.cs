using System.ServiceProcess;

namespace WindowsService
{
    class Program
    {
        static void Main(string[] args)
        {
            
            ServiceBase.Run(new CachalotService());
            
        }
    }
}
