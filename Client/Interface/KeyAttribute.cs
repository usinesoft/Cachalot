using System;

namespace Client.Interface
{
    /// <summary>
    ///     Tag for the optional unique keys
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class KeyAttribute : Attribute
    {
        /// <summary>
        ///     Build a unique key having the specified data type
        /// </summary>
        /// <param name="keyDataType"></param>
        public KeyAttribute(KeyDataType keyDataType = KeyDataType.Default)
        {
            KeyDataType = keyDataType;
        }

        /// <summary>
        ///     int or string key
        /// </summary>
        public KeyDataType KeyDataType { get; }
    }
}