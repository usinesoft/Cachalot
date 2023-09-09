using System;
using System.Reflection;

namespace Tests.TestTools
{
    public class FakeLocalTimeZone : IDisposable
    {
        private readonly TimeZoneInfo _actualLocalTimeZoneInfo;

        public FakeLocalTimeZone(TimeZoneInfo timeZoneInfo)
        {
            _actualLocalTimeZoneInfo = TimeZoneInfo.Local;
            SetLocalTimeZone(timeZoneInfo);
        }

        public void Dispose()
        {
            SetLocalTimeZone(_actualLocalTimeZoneInfo);
        }

        private static void SetLocalTimeZone(TimeZoneInfo timeZoneInfo)
        {
            var info = typeof(TimeZoneInfo).GetField("s_cachedData", BindingFlags.NonPublic | BindingFlags.Static);
            var cachedData = info!.GetValue(null);

            var field = cachedData!.GetType().GetField("_localTimeZone",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            field!.SetValue(cachedData, timeZoneInfo);
        }
    }
}