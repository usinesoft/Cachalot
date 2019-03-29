using System.Collections.Generic;
using Client.Core;
using Client.Interface;

namespace Server
{
    /// <summary>
    /// Less recently used items are removed if the limit capacity is reached
    /// </summary>
    public class LruEvictionPolicy : EvictionPolicy
    {
        private readonly EvictionQueue _evictionQueue = new EvictionQueue();

        public LruEvictionPolicy(int limit, int evictionCount)
        {
            _evictionQueue.Capacity = limit;
            _evictionQueue.EvictionCount = evictionCount;
        }

        public override bool IsEvictionRequired => _evictionQueue.EvictionRequired;


        public override EvictionType Type => EvictionType.LessRecentlyUsed;

        public override void Clear()
        {
            _evictionQueue.Clear();
        }

        public override void AddItem(CachedObject item)
        {
            _evictionQueue.AddNew(item);
        }

        public override IList<CachedObject> DoEviction()
        {
            return _evictionQueue.Go();
        }

        public override void Touch(CachedObject item)
        {
            _evictionQueue.Touch(item);
        }


        public override void TryRemove(CachedObject item)
        {
            _evictionQueue.TryRemove(item);
        }


        public override void Touch(IList<CachedObject> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                _evictionQueue.Touch(items[i]);
            }
        }

        public override string ToString()
        {
            return string.Format("LRU({0}, {1})", _evictionQueue.Capacity, _evictionQueue.EvictionCount);
        }
    }
}