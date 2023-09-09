using System.Collections.Generic;
using Client.Interface;

namespace Client.Core;

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

    public IDictionary<string, CollectionSchema> CollectionsDescriptions { get; set; } =
        new Dictionary<string, CollectionSchema>();

    public void DropCollection(string collectionName)
    {
        if (!CollectionsDescriptions.ContainsKey(collectionName))
            throw new CacheException($"Collection {collectionName} does not exist");

        CollectionsDescriptions.Remove(collectionName);
    }
}