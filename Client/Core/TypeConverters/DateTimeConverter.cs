using System;
using Client.Interface;

namespace Client.Core.TypeConverters
{
    public class DateTimeConverter : IKeyConverter
    {
        public Type SourceType => typeof(DateTime);

        public bool CanConvertToLong => true;

        public bool CanConvertToString => false;

        public long GetAsLong(object key)
        {
            var value = (DateTime) key;

            return value.Ticks;
        }

        public string GetAsString(object key)
        {
            throw new NotImplementedException();
        }
    }
}