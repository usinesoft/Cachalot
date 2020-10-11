using System;
using System.Collections.Generic;
using Client.Core;

namespace Cachalot.Linq
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

        public static void AddExplicitTypeDescription(Type type, ClientSideTypeDescription typeDescription)
        {
            lock (TypeDescriptions)
            {
                TypeDescriptions[type] =  typeDescription;
            }
        }

    }
}