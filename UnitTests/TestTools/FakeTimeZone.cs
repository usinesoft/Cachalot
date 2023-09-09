using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Tests.TestTools
{
    public class FakeLocalTimeZone : IDisposable
    {
        private readonly TimeZoneInfo _actualLocalTimeZoneInfo;

        private static void SetLocalTimeZone(TimeZoneInfo timeZoneInfo)
        {
            var info = typeof(TimeZoneInfo).GetField("s_cachedData", BindingFlags.NonPublic | BindingFlags.Static);
            object cachedData = info!.GetValue(null);

            var field = cachedData!.GetType().GetField("_localTimeZone", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            field!.SetValue(cachedData, timeZoneInfo);
        }

        public FakeLocalTimeZone(TimeZoneInfo timeZoneInfo)
        {
            _actualLocalTimeZoneInfo = TimeZoneInfo.Local;
            SetLocalTimeZone(timeZoneInfo);
        }

        public void Dispose()
        {
            SetLocalTimeZone(_actualLocalTimeZoneInfo);
        }
    }
}
