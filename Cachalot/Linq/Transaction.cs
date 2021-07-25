using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Client.Core;
using Client.Core.Linq;
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

        
        private readonly Connector _connector;

        readonly List<DataRequest> _childRequests = new List<DataRequest>();


        internal Transaction(Connector connector)
        {
            _connector = connector;
            _client = connector.Client;
        }


        public void Put<T>(T item, string collectionName = null)
        {
            collectionName ??= typeof(T).Name;

            var packed = Pack(item, collectionName);

            // add to an existing put non conditional request (for the same collection) or create a new one
            var request = _childRequests.Where(r => r.CollectionName == collectionName && r is PutRequest putRequest && !putRequest.HasCondition).Cast<PutRequest>().FirstOrDefault();

            if (request == null)
            {
                request = new PutRequest(collectionName);

                _childRequests.Add(request);
            }

            request.Items.Add(packed);

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
            collectionName ??= typeof(T).Name;

            var packed = Pack(newValue, collectionName);

            // put requests with condition can contain only one item
            
            var request = new PutRequest(collectionName);

            request.Items.Add(packed);

            _childRequests.Add(request);

            
            var query = PredicateToQuery(test, collectionName);
            
            request.Predicate = query;

        }

        private OrQuery PredicateToQuery<T>(Expression<Func<T, bool>> predicate, string collectionName)
        {
            // convert the predicate to a serializable query
            // create a fake queryable to force query parsing and capture resolution

            var schema = _connector.GetCollectionSchema(collectionName);
            var executor = new NullExecutor(schema, collectionName);
            var queryable = new NullQueryable<T>(executor);

            var unused = queryable.Where(predicate).ToList();

            var query = executor.Expression;
            return query;
        }


        //TODO to be tested
        public void DeleteMany<T>(Expression<Func<T, bool>> where, string collectionName = null)
        {
            collectionName ??= typeof(T).Name;

            var query = PredicateToQuery(where, collectionName);

            var request = new RemoveManyRequest(query);
            
            _childRequests.Add(request);

        }


       
        public void Delete<T>(T item, string collectionName = null)
        {
            collectionName ??= typeof(T).Name;

            var packed = Pack(item, collectionName);

            var request = new RemoveRequest(packed.CollectionName, packed.PrimaryKey);

            _childRequests.Add(request);

        }


        public void Commit()
        {
            _client.ExecuteTransaction(_childRequests);
        }


        private PackedObject Pack<T>(T item, string collectionName = null)
        {
            var schema = _connector.GetCollectionSchema(collectionName);

            if (schema == null)
            {
                throw new CacheException($"Unknown collection {collectionName}. Use Connector.DeclareCollection");
            }

            var packed = PackedObject.Pack(item, schema, collectionName);

            return packed;
        }
    }
} 