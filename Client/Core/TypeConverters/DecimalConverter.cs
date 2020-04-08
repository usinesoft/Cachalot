using System;
using Client.Interface;

namespace Client.Core.TypeConverters
{
    public class DecimalConverter : IKeyConverter
    {
        public Type SourceType => typeof(decimal);

        public bool CanConvertToLong => true;

        public bool CanConvertToString => true;

        public long GetAsLong(object key)
        {
            var value = (decimal) key;
            return (long) (value * 10000);
        }

        public string GetAsString(object key)
        {
            throw new NotImplementedException();
        }
    }
}