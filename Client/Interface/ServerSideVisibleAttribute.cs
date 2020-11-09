using System;

namespace Client.Interface
{
    /// <summary>
    /// Used to tag properties that can be used for server-side aggregation 
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ServerSideVisibleAttribute : Attribute
    {
        

    }
}