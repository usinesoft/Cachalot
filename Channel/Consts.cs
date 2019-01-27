namespace Channel
{
    public static class Consts
    {
        public static readonly byte PingCookie =  55;
        public static readonly byte RequestCookie =  0;
        public static readonly int ClientTimeoutInMilliseconds = 1000;
        public static readonly int StreamingThreshold = 50000;
    }
}