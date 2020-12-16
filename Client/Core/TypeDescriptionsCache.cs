using System;
using System.Collections.Generic;
using Client.Messages;

namespace Client.Core
{
    /// <summary>
    /// Used to minimize reflection when registering dynamic types
    /// </summary>
    public static class TypeDescriptionsCache
    {
        static readonly Dictionary<Type, CollectionSchema> TypeDescriptions = new Dictionary<Type, CollectionSchema>();

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
}