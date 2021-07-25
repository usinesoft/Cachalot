namespace Channel
{
    public static class Constants
    {
        /// <summary>
        /// Prefix of a ping message
        /// </summary>
        public static readonly byte PingCookie = 55;

        /// <summary>
        /// prefix of a request message 
        /// </summary>
        public static readonly byte RequestCookie = 0;
        
        public static readonly int ReceiveTimeoutInMilliseconds = 1000;
        
        public static readonly int ConnectionTimeoutInMilliseconds = 1000;
        
        public static readonly int StreamingThreshold = 50000;
        
        public static readonly int DefaultPort = 48401;
    }
}