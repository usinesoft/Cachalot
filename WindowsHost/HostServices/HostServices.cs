using System;
using System.Collections.Generic;
using Server;
using Host.HostServices.Logger;
using Unity;

namespace Host.HostServices
{

    /// <summary>
    /// Contains technical services implemented by the host (Like log service)
    /// They are exposed througn a Unity container
    /// </summary>
    public static class HostServices
    {
        private static readonly FastLogger Log;


        private static volatile bool _started;

        static HostServices()
        {
            Log = new FastLogger();

            Container.RegisterInstance<ILog>(Log);
        }

        public static UnityContainer Container { get; } = new UnityContainer();

        public static void Start()
        {
            if (_started) throw new NotSupportedException("Can start only once");


            Log.Start();


            _started = true;
        }


        public static void Stop()
        {            
            Log.Stop();
        }

        public static IList<string> GetLog()
        {
            return Log.GetCachedLog();
        }
    }
}