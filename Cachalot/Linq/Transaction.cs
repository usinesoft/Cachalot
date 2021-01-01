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
        private readonly IDataClient _client;

        private readonly List<OrQuery> _conditions = new List<OrQuery>();

        private readonly List<CachedObject> _itemsToDelete = new List<CachedObject>();

        private readonly List<CachedObject> _itemsToPut = new List<CachedObject>();

        private readonly Connector _connector;


        internal Transaction(Connector connector)
        {
            _connector = connector;
            _client = connector.Client;
        }


        public void Put<T>(T item, string collectionName = null)
        {
            collectionName ??= typeof(T).FullName;

            var packed = Pack(item, collectionName);

            _itemsToPut.Add(packed);
            
            
            _conditions.Add(new OrQuery()); // empty condition

        }

        /// <summary>
        ///     Conditional update. The condition is applied server-side on the previous version of the item
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="newValue"></param>
        /// <param name="test"></param>
        /// <param name="collectionName"></param>
        public void UpdateIf<T>(T newValue, Expression<Func<T, bool>> test, string collectionName = null)
        {
            collectionName ??= typeof(T).FullName;

            
            var packed = Pack(newValue, collectionName);

            _itemsToPut.Add(packed);

            
            
            var testAsQuery = ExpressionTreeHelper.PredicateToQuery(test, collectionName);

            _conditions.Add(testAsQuery);

        }


       
        public void Delete<T>(T item, string collectionName = null)
        {
            collectionName ??= typeof(T).FullName;

            var packed = Pack(item, collectionName);

            _itemsToDelete.Add(packed);

        }


        public void Commit()
        {
            _client.ExecuteTransaction(_itemsToPut, _conditions, _itemsToDelete);
        }


        private CachedObject Pack<T>(T item, string collectionName = null)
        {
            var schema = _connector.GetCollectionSchema(collectionName);

            if (schema == null)
            {
                throw new CacheException($"Unknown collection {collectionName}. Use Connector.DeclareCollection");
            }

            var packed = CachedObject.Pack(item, schema, collectionName);

            return packed;
        }
    }
} 