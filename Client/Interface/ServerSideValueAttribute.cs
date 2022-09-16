using Client.Core;
using System;
using System.Runtime.CompilerServices;

namespace Client.Interface
{
    /// <summary>
    ///     Tag for the optional indexation keys
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ServerSideValueAttribute : Attribute
    {
        /// <summary>
        /// </summary>
        public ServerSideValueAttribute(IndexType indexType = IndexType.None, [CallerLineNumber] int lineNumber = -1)
        {
            IndexType = indexType;
            LineNumber = lineNumber;
        }

        public IndexType IndexType { get; }

        public int LineNumber { get; }
    }
}