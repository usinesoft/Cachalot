using System;

namespace Client.Interface
{
    /// <summary>
    ///     Applied to a property, it is converted to text and indexed for full text search
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class FullTextIndexationAttribute : Attribute
    {
    }
}