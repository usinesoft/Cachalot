using System;
using System.Collections.Generic;

namespace Client.Core
{
    /// <summary>
    /// Used to minimize reflection when registering dynamic types
    /// </summary>
    public static class TypeDescriptionsCache
    {
        static readonly Dictionary<Type, ClientSideTypeDescription> TypeDescriptions = new Dictionary<Type, ClientSideTypeDescription>();

        public static ClientSideTypeDescription GetDescription(Type type)
        {
            lock (TypeDescriptions)
            {
                if (TypeDescriptions.TryGetValue(type, out var description)) return description;
               
                description = ClientSideTypeDescription.RegisterType(type);
                TypeDescriptions.Add(type, description);

                return description;
            }
        }

    }
}