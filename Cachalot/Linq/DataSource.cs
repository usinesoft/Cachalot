using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Client.Core;
using Client.Interface;
using Client.Messages;
using Client.Queries;
using JetBrains.Annotations;
using Remotion.Linq;
using Remotion.Linq.Parsing.ExpressionVisitors.Transformation;
using Remotion.Linq.Parsing.Structure;
using Remotion.Linq.Parsing.Structure.NodeTypeProviders;

namespace Cachalot.Linq
{
    /// <summary>
    ///     All data access and update methods. Implements a powerful linq provider
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DataSource<T> : QueryableBase<T>
    {
        private static QueryParser CreateParser()
        {
            //Create Custom node registry
            var customNodeTypeRegistry = new MethodInfoBasedNodeTypeRegistry();

            customNodeTypeRegistry.Register(FullTextSearchExpressionNode.SupportedMethods,
                typeof(FullTextSearchExpressionNode));

            customNodeTypeRegistry.Register(OnlyIfCompleteExpressionNode.SupportedMethods,
                typeof(OnlyIfCompleteExpressionNode));

            //This creates all the default node types
            var nodeTypeProvider = ExpressionTreeParser.CreateDefaultNodeTypeProvider();

            //add custom node provider to the providers
            nodeTypeProvider.InnerProviders.Add(customNodeTypeRegistry);

            var transformerRegistry = ExpressionTransformerRegistry.CreateDefault();
            var processor = ExpressionTreeParser.CreateDefaultProcessor(transformerRegistry);
            var expressionTreeParser = new ExpressionTreeParser(nodeTypeProvider, processor);
            var queryParser = new QueryParser(expressionTreeParser);

            return queryParser;
        }
        //TODO implement truncate

        private readonly ICacheClient _client;
        private readonly ClientSideTypeDescription _typeDescription;

        internal DataSource(ICacheClient client, ClientSideTypeDescription typeDescription)
            : base(CreateParser(), new QueryExecutor(client, typeDescription.AsTypeDescription))
        {
            _client = client;

            _typeDescription = typeDescription;
        }


        public DataSource(IQueryParser queryParser, IQueryExecutor executor)
            : base(new DefaultQueryProvider(typeof(DataSource<>), queryParser, executor))
        {
        }

        public DataSource(IQueryProvider provider, Expression expression)
            : base(provider, expression)
        {
        }


        /// <summary>
        /// Mostly useful in distributed cache mode without persistence. Declare a subset of the data as being fully loaded into the cache
        /// Any expression that can be used for querying is valid here
        /// </summary>
        /// <param name="domainDefinition"></param>
        /// <param name="humanReadableDescription">Optional description of the loaded domain</param>
        public void DeclareLoadedDomain([NotNull] Expression<Func<T, bool>> domainDefinition, string humanReadableDescription = null)
        {
            if (domainDefinition == null) throw new ArgumentNullException(nameof(domainDefinition));


            var query = PredicateToQuery(domainDefinition);

            var domain = new DomainDescription(query, false, humanReadableDescription);

            _client.DeclareDomain(domain);
        }


        /// <summary>
        /// Mostly useful in distributed cache mode without persistence. Declare all instances of a given type as being available
        /// Usually called after the cache is fed. This will enable "get many" operations 
        /// </summary>        
        public void DeclareFullyLoaded(bool fullyLoaded = true)
        {
            
            var domain = new DomainDescription(OrQuery.Empty<T>(), fullyLoaded);

            _client.DeclareDomain(domain);
        }


        /// <summary>
        ///     Get one item by primary key. Returns null if none
        /// </summary>
        /// <param name="primaryKey"></param>
        /// <returns></returns>
        public T this[object primaryKey] => _client.GetOne<T>(primaryKey);


        /// <summary>
        ///     Update or insert an object
        /// </summary>
        /// <param name="item"></param>
        public  void Put(T item)
        {
            _client.Put(item);
        }


        /// <summary>
        ///     Transactionally add a new item only if its not already present (primary key does not already exists)
        /// </summary>
        /// <param name="item"></param>
        /// <returns>true if added, false if already present</returns>
        public bool TryAdd(T item)
        {
            return _client.TryAdd(item);
        }

        /// <summary>
        ///     Delete one item by primary key
        /// </summary>
        /// <param name="item"></param>
        public void Delete(T item)
        {
            var packed = CachedObject.Pack(item, _typeDescription);
            _client.Remove<T>(packed.PrimaryKey);
        }


        /// <summary>
        ///     Remove all the items matching the query
        ///     This method is transactional on a single node cluster
        /// </summary>
        /// <param name="where"></param>
        public void DeleteMany(Expression<Func<T, bool>> where)
        {
            var query = PredicateToQuery(where);

            _client.RemoveMany(query);
        }

        public OrQuery PredicateToQuery(Expression<Func<T, bool>> where)
        {
            // create a fake queryable to force query parsing and capture resolution

            var executor = new NullExecutor(_typeDescription.AsTypeDescription);
            var queryable = new NullQueryable<T>(executor);

            var unused = queryable.Where(where).ToList();

            return executor.Expression;
        }


        /// <summary>
        ///     Conditional update. The item is updated only if the predicate evaluated server-site against the PREVIOUS value is
        ///     true
        ///     Can be used in "optimistic synchronization" scenarios.
        ///     Throws an exception if the condition is not satisfied
        /// </summary>
        /// <param name="newValue"></param>
        /// <param name="test"></param>
        public void UpdateIf(T newValue, Expression<Func<T, bool>> test)
        {
            var testAsQuery = PredicateToQuery(test);

            _client.UpdateIf(newValue, testAsQuery);
        }

        /// <summary>
        ///     Optimistic synchronization using a timestamp property
        ///     Works like an UpdateIf that checks the previous value of a property of type DateTime named "Timestamp"
        ///     It also updates this property withe DateTime.Now
        ///     If you use this you should never modify the timestamp manually
        /// </summary>
        /// <param name="newValue"></param>
        public void UpdateWithTimestampSynchronization(T newValue)
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

            _client.UpdateIf(newValue, orq);
        }


        /// <summary>
        ///     Update or insert a collection of objects. Items are fed by package to optimize network transport.
        ///     For new items an optimized bulk insert algorithm is used
        ///     This method is transactional on a single node cluster
        /// </summary>
        /// <param name="items"></param>
        public void PutMany(IEnumerable<T> items)
        {
            _client.FeedMany(items);
        }
    }
}