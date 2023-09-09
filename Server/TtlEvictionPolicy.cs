using System;
using System.Collections.Generic;
using Client.Core;
using Client.Interface;

namespace Server;

/// <summary>
///     Time To Live eviction policy. The eviction is triggered for items that are older than a specified timespan
/// </summary>
public class TtlEvictionPolicy : EvictionPolicy
{
    private readonly Queue<Tuple<DateTimeOffset, PackedObject>> _evictionQueue = new(1000);

    private readonly HashSet<PackedObject> _removed = new();
    private readonly TimeSpan _timeToLive;


    public TtlEvictionPolicy(TimeSpan timeToLive)
    {
        _timeToLive = timeToLive;
    }

    public override bool IsEvictionRequired =>
        _evictionQueue.Count > 0 && DateTimeOffset.Now - _evictionQueue.Peek().Item1 > _timeToLive;


    public override EvictionType Type => EvictionType.TimeToLive;

    public override void Clear()
    {
        _evictionQueue.Clear();
    }

    public override void AddItem(PackedObject item)
    {
        _evictionQueue.Enqueue(new(DateTimeOffset.Now, item));
    }

    public override IList<PackedObject> DoEviction()
    {
        var toRemove = new List<PackedObject>();

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

    public override void Touch(PackedObject item)
    {
        // nothing to do for this policy
    }


    public override void TryRemove(PackedObject item)
    {
        _removed.Add(item);
    }


    public override void Touch(IList<PackedObject> items)
    {
        // nothing to do for this policy
    }

    public override string ToString()
    {
        return $"TTL(items {_evictionQueue.Count} pending remove {_removed.Count})";
    }
}