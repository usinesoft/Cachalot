using System;
using System.Collections.Generic;
using CoreHost.HostServices.Logger;
using Server;

namespace CoreHost.HostServices
{

    /// <summary>
    /// Contains technical services implemented by the host (Like log service)
    /// 
    /// </summary>
    public static class HostServices
    {
        public static ILog Log { get; }

        private static volatile bool _started;

        static HostServices()
        {
            Log = new FastLogger();
        }

       
        public static void Start(string dataPath)
        {
            if (_started) throw new NotSupportedException("Can start only once");


            ((FastLogger)Log).Start(dataPath);


            _started = true;
        }


        public static void Stop()
        {
            ((FastLogger)Log).Stop();
        }

        public static IList<string> GetLog()
        {
            return ((FastLogger)Log).GetCachedLog();
        }
    }
}