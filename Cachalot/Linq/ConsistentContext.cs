using System;
using System.Collections.Generic;
using System.Linq;

namespace Cachalot.Linq
{

    /// <summary>
    /// A consistent read-only context. All read operations executed in this context are isolated from transactions
    /// </summary>
    public class ConsistentContext
    {
        internal ConsistentContext(Guid sessionId, Connector connector, IEnumerable<string> availableCollections)
        {
            SessionId = sessionId;
            Connector = connector;
            AvailableCollections = new HashSet<string>(availableCollections);
        }

        private HashSet<string> AvailableCollections { get; }

        private Guid SessionId { get; }

        private Connector Connector { get; }

        public IQueryable<T> Collection<T>(string collectionName = null)
        {
            collectionName ??= typeof(T).Name;

            if (!AvailableCollections.Contains(collectionName))
                throw new NotSupportedException($"The collection {collectionName} is not available in this context");

            return Connector.ReadOnlyCollection<T>(SessionId, collectionName);
        }
    }
}