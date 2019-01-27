using System;

namespace Client.Interface
{
    /// <summary>
    ///     Converts arbitrary types to long or string so they can be used as key
    /// </summary>
    public interface IKeyConverter
    {
        /// <summary>
        ///     The type of key that can be converted by this converter
        /// </summary>
        Type SourceType { get; }

        /// <summary>
        ///     Is it able to convert to long
        /// </summary>
        bool CanConvertToLong { get; }

        /// <summary>
        ///     Is it able to convert to string
        /// </summary>
        bool CanConvertToString { get; }

        /// <summary>
        ///     REQUIRE: typeof(key) == SourceType, CanConvertToLong == true
        /// </summary>
        /// <param name="key"> </param>
        /// <returns> </returns>
        long GetAsLong(object key);

        /// <summary>
        ///     REQUIRE: typeof(key) == SourceType, CanConvertToLong == true
        /// </summary>
        /// <param name="key"> </param>
        /// <returns> </returns>
        string GetAsString(object key);
    }
}