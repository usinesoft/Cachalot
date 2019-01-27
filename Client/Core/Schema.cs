using System.Collections.Generic;
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

        public IList<TypeDescription> TypeDescriptions { get; set; } = new List<TypeDescription>();
    }
}