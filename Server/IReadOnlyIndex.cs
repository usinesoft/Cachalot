using System.Collections.Generic;
using Client.Core;
using Client.Queries;

namespace Server;

public interface IReadOnlyIndex
{
    string Name { get; }

    IndexType IndexType { get; }

    ISet<PackedObject> GetMany(IList<KeyValue> values, QueryOperator op = QueryOperator.Eq);

    /// <summary>
    ///     Used only in full scan mode
    /// </summary>
    /// <returns></returns>
    IEnumerable<PackedObject> GetAll(bool descendingOrder = false, int maxCount = 0);

    /// <summary>
    ///     Count exact (or quickly estimate if <see cref="fastEstimate" />) the number of items matching the atomic query
    /// </summary>
    /// <param name="values"></param>
    /// <param name="op"></param>
    /// <param name="fastEstimate">Used in the planning phase to choose the indexes to use</param>
    /// <returns></returns>
    int GetCount(IList<KeyValue> values, QueryOperator op = QueryOperator.Eq, bool fastEstimate = false);
}