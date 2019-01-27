using System;
using Client.Interface;

namespace Client.Core.TypeConverters
{
    public class FloatConverter : IKeyConverter
    {
        public Type SourceType => typeof(float);

        public bool CanConvertToLong => true;

        public bool CanConvertToString => false;

        public long GetAsLong(object key)
        {
            var value = (float) key;
            return (long) (value * 10000);
        }

        public string GetAsString(object key)
        {
            throw new NotImplementedException();
        }
    }
}