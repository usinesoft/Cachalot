using System;
using System.Collections.Generic;
using Client.Core;

namespace Server;

/// <summary>
///     Mixed data structure. Can be used both as linked list and dictionary
///     Used to manage the eviction priority for cached items
///     The eviction candidates are at the beginning of the list
///     If an item is used it is moved at the end of the list
/// </summary>
public class EvictionQueue
{
    private readonly Dictionary<KeyValue, LinkedListNode<PackedObject>> _cachedObjectsByKey;
    private readonly LinkedList<PackedObject> _queue;


    private readonly object _syncRoot = new();

    public EvictionQueue()
    {
        _cachedObjectsByKey = new();
        _queue = new();
    }

    /// <summary>
    ///     When eviction is needed <see cref="EvictionCount" /> items are removed
    /// </summary>
    public int EvictionCount { get; set; }

    /// <summary>
    ///     Maximum capacity (if more items are added) eviction is needed
    /// </summary>
    public int Capacity { get; set; }

    public int Count
    {
        get
        {
            lock (_syncRoot)
            {
                return _cachedObjectsByKey.Count;
            }
        }
    }

    public bool EvictionRequired
    {
        get
        {
            lock (_syncRoot)
            {
                return _cachedObjectsByKey.Count >= Capacity;
            }
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _cachedObjectsByKey.Clear();
            _queue.Clear();
        }
    }

    /// <summary>
    ///     Add a new item to the eviction queue. The item is stored at the end (less likely to be evicted)
    ///     REQUIRE: Item not already present in the queue
    /// </summary>
    /// <param name="newItem"></param>
    public void AddNew(PackedObject newItem)
    {
        if (newItem == null) throw new ArgumentNullException(nameof(newItem));

        lock (_syncRoot)
        {
            if (_cachedObjectsByKey.ContainsKey(newItem.PrimaryKey))
                throw new NotSupportedException("Item already in eviction queue");

            var lastNode = _queue.AddLast(newItem);
            _cachedObjectsByKey.Add(newItem.PrimaryKey, lastNode);
        }
    }

    /// <summary>
    ///     Proceed to eviction (the first <see cref="EvictionCount" /> items will be removed
    /// </summary>
    /// <returns>The items removed</returns>
    public IList<PackedObject> Go()
    {
        var result = new List<PackedObject>(EvictionCount);
        if (!EvictionRequired)
            return result;

        lock (_syncRoot)
        {
            var currentCount = 0;
            var node = _queue.First;

            //remove more the Capacity - Count to avoid the eviction to be triggered for each added item
            var itemsToRemove = Count - Capacity + EvictionCount;
            if (itemsToRemove <= 0)
                return result;

            while (currentCount < itemsToRemove && node.Next != null)
            {
                var nextNode = node.Next;

                _queue.Remove(node);
                _cachedObjectsByKey.Remove(node.Value.PrimaryKey);
                result.Add(node.Value);

                node = nextNode;
                currentCount++;
            }
        }

        return result;
    }


    /// <summary>
    ///     Remove an item if it is present in the queue
    /// </summary>
    /// <param name="itemToRemove"></param>
    public void TryRemove(PackedObject itemToRemove)
    {
        if (itemToRemove == null) throw new ArgumentNullException(nameof(itemToRemove));

        lock (_syncRoot)
        {
            if (_cachedObjectsByKey.ContainsKey(itemToRemove.PrimaryKey))
            {
                var node = _cachedObjectsByKey[itemToRemove.PrimaryKey];
                _queue.Remove(node);
                _cachedObjectsByKey.Remove(itemToRemove.PrimaryKey);
            }
        }
    }

    /// <summary>
    ///     Mark the item as used. Moves it at the end of the queue
    ///     If the item is not present ignore (may be useful if certain items are excluded by the eviction policy)
    /// </summary>
    /// <param name="item"></param>
    public void Touch(PackedObject item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));

        lock (_syncRoot)
        {
            if (_cachedObjectsByKey.ContainsKey(item.PrimaryKey))
            {
                var node = _cachedObjectsByKey[item.PrimaryKey];

                _queue.Remove(node);
                _queue.AddLast(node);
            }
        }
    }
}