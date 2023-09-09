using System;
using System.Collections.Generic;

namespace Client.Core;

/// <summary>
///     Used to minimize reflection when registering dynamic types
/// </summary>
public static class TypeDescriptionsCache
{
    private static readonly Dictionary<Type, CollectionSchema> TypeDescriptions = new();

    public static CollectionSchema GetDescription(Type type)
    {
        lock (TypeDescriptions)
        {
            if (TypeDescriptions.TryGetValue(type, out var description)) return description;

            description = TypedSchemaFactory.FromType(type);
            TypeDescriptions.Add(type, description);

            return description;
        }
    }
}