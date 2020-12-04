using System.Collections.Generic;
using System.Security;
using Client.Messages;

namespace Client.Core
{
    public class Schema
    {
        /// <summary>
        ///     Index of this node in the cluster
        /// </summary>
        public int ShardIndex { get; set; }

        /// <summary>
        ///     Number of nodes in the cluster
        /// </summary>
        public int ShardCount { get; set; }

        public IDictionary<string,  CollectionSchema> CollectionsDescriptions { get; set; } = new Dictionary<string, CollectionSchema>();
    }
}