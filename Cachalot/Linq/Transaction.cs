using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Client.Core;
using Client.Interface;
using Client.Messages;
using Client.Queries;

namespace Cachalot.Linq
{
    /// <summary>
    ///     A transaction contains Put and Delete operations. They will be executed in an ACID transaction
    /// </summary>
    public class Transaction
    {
        private readonly ICacheClient _client;

        private readonly List<OrQuery> _conditions = new List<OrQuery>();

        private readonly List<CachedObject> _itemsToDelete = new List<CachedObject>();

        private readonly List<CachedObject> _itemsToPut = new List<CachedObject>();

        HashSet<Type> _types = new HashSet<Type>();


        internal Transaction(ICacheClient client)
        {
            _client = client;
        }


        public void Put<T>(T item)
        {
            var packed = Pack(item);

            _itemsToPut.Add(packed);
            _conditions.Add(new OrQuery()); // empty condition

            _types.Add(typeof(T));
        }

        /// <summary>
        ///     Conditional update. The condition is applied server-side on the previous version of the item
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="newValue"></param>
        /// <param name="test"></param>
        public void UpdateIf<T>(T newValue, Expression<Func<T, bool>> test)
        {
            var desc = TypeDescriptionsCache.GetDescription(typeof(T));

            var packed = Pack(newValue);

            _itemsToPut.Add(packed);

            var dataSource = new DataSource<T>(_client, desc);
            var testAsQuery = dataSource.PredicateToQuery(test);

            _conditions.Add(testAsQuery);

            _types.Add(typeof(T));
        }


        //TODO add unit test for timestamp synchronization (coverage)
        /// <summary>
        ///     Optimistic synchronization using a timestamp property
        ///     Works like an UpdateIf that checks the previous value of a property of type DateTime named "Timestamp"
        ///     It also updates this property withe DateTime.Now
        ///     If you use this you should never modify the timestamp manually when updating the object
        /// </summary>
        /// <param name="newValue"></param>
        public void UpdateWithTimestampSynchronization<T>(T newValue)
        {
            var prop = newValue.GetType().GetProperty("Timestamp");
            if (prop == null) throw new CacheException($"No Timestamp property found on type {typeof(T).Name}");

            if (!prop.CanWrite)
                throw new CacheException($"The Timestamp property of type {typeof(T).Name} is not writable");

            var oldTimestamp = prop.GetValue(newValue);

            var kv = KeyInfo.ValueToKeyValue(oldTimestamp,
                new KeyInfo(KeyDataType.IntKey, KeyType.ScalarIndex, "Timestamp"));

            var q = new AtomicQuery(kv);
            var andQuery = new AndQuery();
            andQuery.Elements.Add(q);
            var orq = new OrQuery(typeof(T));
            orq.Elements.Add(andQuery);

            var now = DateTime.Now;
            var newTimestamp = now.AddTicks(1); // add one to be sure its different


            prop.SetValue(newValue, newTimestamp);

            var packed = Pack(newValue);

            _itemsToPut.Add(packed);

            _conditions.Add(orq);
        }


        public void Delete<T>(T item)
        {
            var packed = Pack(item);

            _itemsToDelete.Add(packed);

            _types.Add(typeof(T));
        }


        public void Commit()
        {
            foreach (var type in _types)
            {
                var description = TypeDescriptionsCache.GetDescription(type);
                _client.RegisterType(type, description);

            }
            _client.ExecuteTransaction(_itemsToPut, _conditions, _itemsToDelete);
        }


        private CachedObject Pack<T>(T item)
        {
            var packed = CachedObject.Pack(item, TypeDescriptionsCache.GetDescription(typeof(T)));
            return packed;
        }
    }
}