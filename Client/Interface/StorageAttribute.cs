using System;

namespace Client.Interface
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class StorageAttribute : Attribute
    {
        public StorageAttribute(bool useCompression)
        {
            UseCompression = useCompression;
        }

        public bool UseCompression { get; }
    }
}