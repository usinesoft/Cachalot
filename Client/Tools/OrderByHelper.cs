using System;
using System.Collections.Generic;
using System.Linq;
using Client.Core;
using Newtonsoft.Json.Linq;

namespace Client.Tools
{
    public static class OrderByHelper
    {
        /// <summary>
        /// Mix many ordered IEnumerable into an ordered result
        /// </summary>
        /// <param name="orderedPropertyName">Te name of the property that we order by</param>
        /// <param name="sources"></param>
        /// <returns></returns>
        public static IEnumerable<RankedItem> MixEnumerable(string orderedPropertyName, params IEnumerable<RankedItem>[] sources)
        {
            return MergePreserveOrder4(sources, ri => (JValue) ri.Item.GetValue(orderedPropertyName));

        }


        /// <summary>
        /// Thank you Stack Overflow
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TOrder"></typeparam>
        /// <param name="aa"></param>
        /// <param name="orderFunc"></param>
        /// <returns></returns>
        private static IEnumerable<T> MergePreserveOrder4<T, TOrder>(
            this IEnumerable<IEnumerable<T>> aa,
            Func<T, TOrder> orderFunc) where TOrder : IComparable<TOrder>
        {
            var items = aa.Select(xx => xx.GetEnumerator())
                .Where(ee => ee.MoveNext())
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
                    {
                        if (value.CompareTo(items[ii].Item1) <= 0)
                        {   // NB: using a tuple to minimize calls to orderFunc
                            items.Insert(ii, Tuple.Create(value, next.Item2));
                            break;
                        }
                    }

                    if (ii == items.Count) items.Add(Tuple.Create(value, next.Item2));
                }
                else next.Item2.Dispose(); 
            }
        }
    }
}