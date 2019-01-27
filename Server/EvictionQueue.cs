using System;
using System.Collections.Generic;
using Client.Core;

namespace Server
{
    /// <summary>
    /// Mixed data structure. Can be used both as linked list and dictionary
    /// Used to manage the eviction priority for cached items
    /// The eviction candidates are at the begining of the list
    /// If an item is used it is moved at the end of the list
    /// </summary>
    public class EvictionQueue
    {
        private readonly Dictionary<KeyValue, LinkedListNode<CachedObject>> _cachedObjectsByKey;
        private readonly LinkedList<CachedObject> _queue;


        readonly object _syncRoot = new object();

        /// <summary>
        /// Maximum capacity (if more items are added) eviction is needed
        /// </summary>
        private int _capacity;

        /// <summary>
        /// The quanity of items evicted 
        /// </summary>
        private int _evictionCount;

        public EvictionQueue()
        {
            _cachedObjectsByKey = new Dictionary<KeyValue, LinkedListNode<CachedObject>>();
            _queue = new LinkedList<CachedObject>();
        }

        /// <summary>
        /// When eviction is needed <see cref="EvictionCount"/> items are removed
        /// </summary>
        public int EvictionCount
        {
            get { return _evictionCount; }
            set { _evictionCount = value; }
        }

        /// <summary>
        /// Maximum capacity (if more items are added) eviction is needed
        /// </summary>
        public int Capacity
        {
            get { return _capacity; }
            set { _capacity = value; }
        }

        public int Count
        {
            get { return _cachedObjectsByKey.Count; }
        }

        public bool EvictionRequired
        {
            get
            {
                lock (_syncRoot)
                {
                    return _cachedObjectsByKey.Count > Capacity;
                }
            }
        }

        public void Clear()
        {
            _cachedObjectsByKey.Clear();
            _queue.Clear();
        }

        /// <summary>
        /// Add a new item to the eviction queue. The item is stored at the end (less likely to be evicted)
        /// REQUIRE: Item not already present in the queue
        /// </summary>
        /// <param name="newItem"></param>
        public void AddNew(CachedObject newItem)
        {
            if (newItem == null) throw new ArgumentNullException("newItem");

            lock (_syncRoot)
            {
                if (_cachedObjectsByKey.ContainsKey(newItem.PrimaryKey))
                    throw new NotSupportedException("Item already in eviction queue");

                LinkedListNode<CachedObject> lastNode = _queue.AddLast(newItem);
                _cachedObjectsByKey.Add(newItem.PrimaryKey, lastNode);
            }
        }

        /// <summary>
        /// Proceed to eviction (the first <see cref="EvictionCount"/> items will be removed
        /// </summary>
        /// <returns>The items removed</returns>
        public IList<CachedObject> GO()
        {
            List<CachedObject> result = new List<CachedObject>(_evictionCount);
            lock (_syncRoot)
            {
                int currentCount = 0;
                LinkedListNode<CachedObject> node = _queue.First;

                //remove more the Capacity - Count to avoit the eviction to be triggered for each added item
                int itemsToRemove = Count - Capacity + _evictionCount;
                if (itemsToRemove <= 0)
                    return result;

                while ((currentCount < itemsToRemove) && (node.Next != null))
                {
                    LinkedListNode<CachedObject> nextNode = node.Next;

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
        /// Remove an item if it is present in the queue
        /// </summary>
        /// <param name="itemToRemove"></param>
        public void TryRemove(CachedObject itemToRemove)
        {
            if (itemToRemove == null) throw new ArgumentNullException("itemToRemove");

            lock (_syncRoot)
            {
                if (_cachedObjectsByKey.ContainsKey(itemToRemove.PrimaryKey))
                {
                    LinkedListNode<CachedObject> node = _cachedObjectsByKey[itemToRemove.PrimaryKey];
                    _queue.Remove(node);
                    _cachedObjectsByKey.Remove(itemToRemove.PrimaryKey);
                }
            }
        }

        /// <summary>
        /// Mark the item as used. Moves it at the end of the queue
        /// If the item is not present ignore (may be useful if certain items are excluded by the eviction policy)
        /// </summary>
        /// <param name="item"></param>
        public void Touch(CachedObject item)
        {
            if (item == null) throw new ArgumentNullException("item");

            lock (_syncRoot)
            {
                if (_cachedObjectsByKey.ContainsKey(item.PrimaryKey))
                {
                    LinkedListNode<CachedObject> node = _cachedObjectsByKey[item.PrimaryKey];

                    _queue.Remove(node);
                    _queue.AddLast(node);
                }
            }
        }
    }
}