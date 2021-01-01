using System;
using System.Collections.Generic;
using Client.Core;
using Client.Interface;

namespace Server
{
    /// <summary>
    ///     Less recently used items are removed if the limit capacity is reached
    /// </summary>
    public class LruEvictionPolicy : EvictionPolicy
    {
        private readonly EvictionQueue _evictionQueue = new EvictionQueue();

        public LruEvictionPolicy(int limit, int evictionCount)
        {
            if(limit <= 0)
                throw new ArgumentException($"the {nameof(limit)} should be strictly positive", nameof(limit));

            if(evictionCount <= 0)
                throw new ArgumentException($"the {nameof(evictionCount)} should be strictly positive", nameof(evictionCount));

            if(limit <= evictionCount)
                throw new ArgumentException($"the {nameof(limit)} should be strictly superior to {nameof(evictionCount)}");


            _evictionQueue.Capacity = limit;
            _evictionQueue.EvictionCount = evictionCount;
        }

        public override bool IsEvictionRequired => _evictionQueue.EvictionRequired;


        public override EvictionType Type => EvictionType.LessRecentlyUsed;

        public override void Clear()
        {
            _evictionQueue.Clear();
        }

        public override void AddItem(PackedObject item)
        {
            _evictionQueue.AddNew(item);
        }

        public override IList<PackedObject> DoEviction()
        {
            return _evictionQueue.Go();
        }

        public override void Touch(PackedObject item)
        {
            _evictionQueue.Touch(item);
        }


        public override void TryRemove(PackedObject item)
        {
            _evictionQueue.TryRemove(item);
        }


        public override void Touch(IList<PackedObject> items)
        {
            foreach (var t in items)
                _evictionQueue.Touch(t);
        }

        public override string ToString()
        {
            return $"LRU({_evictionQueue.Capacity}, {_evictionQueue.EvictionCount})";
        }
    }
}