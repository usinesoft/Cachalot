using System;
using System.Collections.Generic;
using System.Linq;
using Client.Core;

namespace Client.Tools;

public static class OrderByHelper
{
    /// <summary>
    ///     Mix many ordered IEnumerable into an ordered result
    /// </summary>
    /// <param name="orderedPropertyName">Te name of the property that we order by</param>
    /// <param name="descending"></param>
    /// <param name="sources"></param>
    /// <returns></returns>
    public static IEnumerable<T> MixOrderedEnumerators<T>(string orderedPropertyName, bool descending = false,
                                                                params IEnumerator<T>[] sources) where T : IWithComparableMember
    {
        return descending
            ? MergePreserveOrderDescending(sources,
                ri => ri.GetComparableMember(orderedPropertyName))
            : MergePreserveOrderAscending(sources,
                ri => ri.GetComparableMember(orderedPropertyName));
    }


    private static IEnumerable<T> MergePreserveOrderAscending<T, TOrder>(
        this IEnumerable<IEnumerator<T>> inputs,
        Func<T, TOrder> orderFunc) where TOrder : IComparable
    {
        var items = inputs.Where(ee => ee.MoveNext())
            .Select(ee => Tuple.Create(orderFunc(ee.Current), ee))
            .OrderBy(ee => ee.Item1).ToList();

        while (items.Count > 0)
        {
            yield return items[0].Item2.Current;

            var next = items[0];
            items.RemoveAt(0);
            if (next.Item2.MoveNext())
            {
                var value = orderFunc(next.Item2.Current);
                var ii = 0;
                for (; ii < items.Count; ++ii)
                    if (value.CompareTo(items[ii].Item1) <= 0)
                    {
                        // NB: using a tuple to minimize calls to orderFunc
                        items.Insert(ii, Tuple.Create(value, next.Item2));
                        break;
                    }

                if (ii == items.Count) items.Add(Tuple.Create(value, next.Item2));
            }
            
        }
    }

    private static IEnumerable<T> MergePreserveOrderDescending<T, TOrder>(
        this IEnumerable<IEnumerator<T>> inputs,
        Func<T, TOrder> orderFunc) where TOrder : IComparable
    {
        var items = inputs.Where(ee => ee.MoveNext())
            .Select(ee => Tuple.Create(orderFunc(ee.Current), ee))
            .OrderByDescending(ee => ee.Item1).ToList();

        while (items.Count > 0)
        {
            yield return items[0].Item2.Current;

            var next = items[0];
            items.RemoveAt(0);
            if (next.Item2.MoveNext())
            {
                var value = orderFunc(next.Item2.Current);
                var ii = 0;
                for (; ii < items.Count; ++ii)
                    if (value.CompareTo(items[ii].Item1) > 0)
                    {
                        // NB: using a tuple to minimize calls to orderFunc
                        items.Insert(ii, Tuple.Create(value, next.Item2));
                        break;
                    }

                if (ii == items.Count) items.Add(Tuple.Create(value, next.Item2));
            }
            
        }
    }
}