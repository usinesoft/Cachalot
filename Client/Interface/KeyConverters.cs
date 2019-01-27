#region

using System;
using Client.Core.TypeConverters;

#endregion

namespace Client.Interface
{
    /// <summary>
    ///     Repository of key converters
    /// </summary>
    public static class KeyConverters
    {
        /// <summary>
        ///     Get the <see cref="IKeyConverter" /> for a specified type if available
        /// </summary>
        /// <param name="srcType"> </param>
        /// <returns> </returns>
        public static IKeyConverter GetIfAvailable(Type srcType)
        {
            if (srcType == typeof(float)) return new FloatConverter();

            if (srcType == typeof(double)) return new DoubleConverter();

            if (srcType == typeof(decimal)) return new DecimalConverter();

            if (srcType == typeof(DateTime)) return new DateTimeConverter();
            return null;
        }
    }
}