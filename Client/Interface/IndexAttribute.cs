using System;

namespace Client.Interface
{
    /// <summary>
    ///     Tag for the optional indexation keys
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class IndexAttribute : Attribute
    {
        
        /// <summary>
        /// </summary>
        /// <param name="keyDataType">int or string key</param>
        /// <param name="ordered">if true, comparison operators can be applied</param>
        public IndexAttribute(KeyDataType keyDataType = KeyDataType.Default, bool ordered = false)
        {
            KeyDataType = keyDataType;
            Ordered = ordered;
        }


        /// <summary>
        ///     int or string key
        /// </summary>
        public KeyDataType KeyDataType { get; }

        /// <summary>
        ///     If true then comparison operators can be applied
        /// </summary>
        public bool Ordered { get; }
    }
}