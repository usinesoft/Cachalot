using System;
using Client.Interface;

namespace Client.Core.TypeConverters
{
    public class DateTimeOffsetConverter : IKeyConverter
    {
        public Type SourceType => typeof(DateTimeOffset);

        public bool CanConvertToLong => true;

        public bool CanConvertToString => false;

        public long GetAsLong(object key)
        {
            var value = (DateTimeOffset) key;

            return value.Ticks;
        }

        public string GetAsString(object key)
        {
            throw new NotImplementedException();
        }
    }
}