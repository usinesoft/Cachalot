using System;
using Client.Interface;

namespace Client.Core.TypeConverters
{
    public class DoubleConverter : IKeyConverter
    {
        public Type SourceType => typeof(double);

        public bool CanConvertToLong => true;

        public bool CanConvertToString => false;

        public long GetAsLong(object key)
        {
            var value = (double) key;
            return (long) (value * 10000);
        }

        public string GetAsString(object key)
        {
            throw new NotImplementedException();
        }
    }
}