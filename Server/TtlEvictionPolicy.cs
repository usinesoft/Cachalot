using System;
using System.Collections.Generic;
using Client.Core;
using Client.Interface;

namespace Server
{

    /// <summary>
    /// Time To Live eviction policy. The eviction is triggered for items that are older than a specified timespan
    /// </summary>
    public class TtlEvictionPolicy : EvictionPolicy
    {
        private readonly TimeSpan _timeToLive;
        private readonly Queue<Tuple<DateTimeOffset, CachedObject>> _evictionQueue = new Queue<Tuple<DateTimeOffset, CachedObject>>(1000);

        private readonly HashSet<CachedObject> _removed = new HashSet<CachedObject>();

        /// <summary>
        /// Mostly for tests
        /// </summary>
        public int PendingRemoveCount => _removed.Count;

        public TtlEvictionPolicy(TimeSpan timeToLive)
        {
            _timeToLive = timeToLive;
        }

        public override bool IsEvictionRequired => _evictionQueue.Count > 0 && DateTimeOffset.Now - _evictionQueue.Peek().Item1 > _timeToLive ;


        public override EvictionType Type => EvictionType.TimeToLive;

        public override void Clear()
        {
            _evictionQueue.Clear();
        }

        public override void AddItem(CachedObject item)
        {
            _evictionQueue.Enqueue(new Tuple<DateTimeOffset, CachedObject>(DateTimeOffset.Now, item));
        }

        public override IList<CachedObject> DoEviction()
        {

            List<CachedObject> toRemove = new List<CachedObject>();

            var now = DateTimeOffset.Now;
            while (_evictionQueue.Count > 0)
            {
                var oldest = _evictionQueue.Peek();
                if (_removed.Contains(oldest.Item2)) // it was explicitly removed
                {
                    _evictionQueue.Dequeue();
                    _removed.Remove(oldest.Item2);
                }
                else if (now - oldest.Item1 > _timeToLive) // it expired
                {
                    toRemove.Add(oldest.Item2);
                    _evictionQueue.Dequeue();
                }
                else
                {
                    break;
                }
            }

            return toRemove;
        }

        public override void Touch(CachedObject item)
        {
            // nothing to do for this policy
        }


        public override void TryRemove(CachedObject item)
        {
            _removed.Add(item);
        }


        public override void Touch(IList<CachedObject> items)
        {
            // nothing to do for this policy
        }

        public override string ToString()
        {
            return $"TTL(items {_evictionQueue.Count} pending remove {_removed.Count})";
        }
    }
}