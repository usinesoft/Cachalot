using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Cachalot.Linq;
using Client.Core;
using Client.Interface;
using Client.Messages;
using Newtonsoft.Json.Linq;

namespace UnitTests
{

    /// <summary>
    /// Extension methods used to adapt the old unit tests after refactoring
    /// </summary>
    internal static class UtExtensions
    {

        public static CollectionSchema DeclareCollection<T>(this IDataClient @this)
        {
            var description = TypeDescriptionsCache.GetDescription(typeof(T));
            var schema = description.AsCollectionSchema;

            @this.DeclareCollection(description.FullTypeName, schema);

            return schema;
        }

        public static IEnumerable<T> GetMany<T>(this IDataClient @this, Expression<Func<T, bool>> where)
        {
            var dataSource = new DataSource<T>(null);
            var query = dataSource.PredicateToQuery(where);

            
            return @this.GetMany(query).Select(ri => ((JObject) ri.Item).ToObject<T>(SerializationHelper.Serializer));
        }

        public static T GetOne<T>(this IDataClient @this, Expression<Func<T, bool>> where)
        {
            return @this.GetMany<T>(where).FirstOrDefault();
        }

        public static void PutMany<T>(this IDataClient @this, IEnumerable<T> items, bool excludeFromEviction = false)
        {
            var description = TypeDescriptionsCache.GetDescription(typeof(T));
            var schema = description.AsCollectionSchema;

            @this.FeedMany(schema.CollectionName, items.Select(i=> CachedObject.Pack(i, description)), excludeFromEviction);
        }

        public static void PutOne<T>(this IDataClient @this, T item, bool excludeFromEviction = false)
        {
            var description = TypeDescriptionsCache.GetDescription(typeof(T));
            var schema = description.AsCollectionSchema;

            @this.Put(schema.CollectionName, CachedObject.Pack(item, description), excludeFromEviction);
        }

        public static int RemoveMany<T>(this IDataClient @this, Expression<Func<T, bool>> where)
        {
            var dataSource = new DataSource<T>(null);
            var query = dataSource.PredicateToQuery(where);

            return @this.RemoveMany(query);
        }

        public static void DeclareDomain<T>(this IDataClient @this, Expression<Func<T, bool>> where)
        {
            var dataSource = new DataSource<T>(null);
            var query = dataSource.PredicateToQuery(where);

            @this.DeclareDomain(new DomainDescription(query));
        }

        public static int Count<T>(this IDataClient @this, Expression<Func<T, bool>> where)
        {
            var dataSource = new DataSource<T>(null);
            var query = dataSource.PredicateToQuery(where);

            return @this.EvalQuery(query).Item2;
        }

        public static bool IsComplete<T>(this IDataClient @this, Expression<Func<T, bool>> where)
        {
            var dataSource = new DataSource<T>(null);
            var query = dataSource.PredicateToQuery(where);

            return @this.EvalQuery(query).Item1;
        }
    }
}