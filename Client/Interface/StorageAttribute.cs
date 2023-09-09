using System;
using Client.Core;

namespace Client.Interface;

[AttributeUsage(AttributeTargets.Class)]
public sealed class StorageAttribute : Attribute
{
    public StorageAttribute(Layout storageLayout)
    {
        StorageLayout = storageLayout;
    }

    public Layout StorageLayout { get; }
}