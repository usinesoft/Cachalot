using System;

namespace Client.Interface
{
    /// <summary>
    ///     Tag for the one and only primary key property of a class
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class PrimaryKeyAttribute : Attribute
    {
        /// <summary>
        /// </summary>
        /// <param name="keyDataType">int or string key</param>
        public PrimaryKeyAttribute(KeyDataType keyDataType = KeyDataType.Default)
        {
            KeyDataType = keyDataType;
        }

        /// <summary>
        ///     int or string key
        /// </summary>
        public KeyDataType KeyDataType { get; }
    }
}