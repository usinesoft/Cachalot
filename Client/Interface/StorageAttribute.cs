using Client.Core;
using System;

namespace Client.Interface
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class StorageAttribute : Attribute
    {
        public StorageAttribute(Layout  storageLayout)
        {
            StorageLayout = storageLayout;
        }

        public Layout StorageLayout{ get; }
    }
}