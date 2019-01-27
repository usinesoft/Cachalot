using System.Collections.Generic;
using Client.Core;
using Client.Messages;

namespace Server
{
    /// <summary>
    ///     This request does not need to be serializable. It is only used internally by the server
    /// </summary>
    internal class EvictionRequest : DataRequest
    {
        public EvictionRequest(string fullTypeName, IList<CachedObject> itemsToRemove)
            : base(DataAccessType.Write, fullTypeName)
        {
            ItemsToRemove = itemsToRemove;
        }


        public IList<CachedObject> ItemsToRemove { get; }
    }
}