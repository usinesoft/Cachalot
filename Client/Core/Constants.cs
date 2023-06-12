namespace Client.Core
{
    public static class Constants
    {
        /// <summary>
        /// Prefix of a ping message
        /// </summary>
        public static readonly byte PingCookie = 55;
        
        /// <summary>
        /// Proactive close request
        /// </summary>
        public static readonly byte CloseCookie = 75;

        /// <summary>
        /// prefix of a request message 
        /// </summary>
        public static readonly byte RequestCookie = 0;

        public static readonly int ReceiveTimeoutInMilliseconds = 1000;

        public static readonly int ConnectionTimeoutInMilliseconds = 3000;

        public static readonly int StreamingThreshold = 50000;

        public static readonly int DefaultPort = 48401;

        public static readonly int DefaultPoolCapacity = 4;

        public static readonly int DefaultPreloadedConnections = 1;

    }
}