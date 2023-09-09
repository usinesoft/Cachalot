using Client.Core;

namespace Client.Interface;

public class CollectionSummary
{
    public string Name { get; set; }

    public long ItemsCount { get; set; }

    public Layout StorageLayout { get; set; }

    public EvictionType EvictionType { get; set; }

    public bool FullTextSearch { get; set; }
}