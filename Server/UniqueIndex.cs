using System;
using System.Collections.Generic;
using System.Linq;
using Client.Core;
using Client.Queries;
using JetBrains.Annotations;

namespace Server;

/// <summary>
///     Wrap a simple dictionary as a read-only index. Useful for uniform access to all index types
/// </summary>
internal class UniqueIndex : IReadOnlyIndex
{
    private readonly IDictionary<KeyValue, PackedObject> _dictionary;

    public UniqueIndex(string name, IDictionary<KeyValue, PackedObject> dictionary)
    {
        _dictionary = dictionary;
        Name = name;
    }

    public string Name { get; }

    public IndexType IndexType => IndexType.Primary;

    public ISet<PackedObject> GetMany([NotNull] IList<KeyValue> values, QueryOperator op = QueryOperator.Eq)
    {
        if (values == null) throw new ArgumentNullException(nameof(values));
        if (values.Count == 0) throw new ArgumentException("Value cannot be an empty collection.", nameof(values));

        if (op != QueryOperator.Eq)
            throw new ArgumentException("Only equality operator can be applied on a unique index");

        var result = new HashSet<PackedObject>();


        foreach (var keyValue in values)
            if (_dictionary.TryGetValue(keyValue, out var value))
                result.Add(value);


        return result;
    }

    public IEnumerable<PackedObject> GetAll(bool descendingOrder = false, int maxCount = 0)
    {
        if (descendingOrder) throw new NotSupportedException("Descending order can be used only on ordered indexes");

        return maxCount == 0 ? _dictionary.Values : _dictionary.Values.Take(maxCount);
    }

    /// <summary>
    ///     Operator and fastEstimate are ignored for dictionary indexes
    /// </summary>
    /// <param name="values"></param>
    /// <param name="op"></param>
    /// <param name="fastEstimate"></param>
    /// <returns></returns>
    public int GetCount(IList<KeyValue> values, QueryOperator op = QueryOperator.Eq, bool fastEstimate = false)
    {
        var result = 0;

        foreach (var keyValue in values)
            if (_dictionary.ContainsKey(keyValue))
                result++;

        return result;
    }
}