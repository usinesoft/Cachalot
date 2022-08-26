using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Client.Core;
using Client.Core.Linq;
using Client.Interface;
using Client.Messages;
using Client.Messages.Pivot;
using Client.Parsing;
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
        public void Truncate()
        {
            _client.Truncate(_collectionName);
        }

        private readonly IDataClient _client;

        /// <summary>
        ///     This one is mandatory
        /// </summary>
        private readonly CollectionSchema _collectionSchema;


        private readonly string _collectionName;

        private PackedObject Pack(T item)
        {
            return PackedObject.Pack(item, _collectionSchema, _collectionName);
        }

        /// <summary>
        ///     If all the parameters are null the DataSource can be used only to convert an expression to a query, not to
        ///     manipulate data
        /// </summary>
        /// <param name="connector"></param>
        /// <param name="collectionName"></param>
        /// <param name="collectionSchema"></param>
        /// <param name="sessionId"></param>
        internal DataSource([NotNull] Connector connector, [NotNull] string collectionName,
            [NotNull] CollectionSchema collectionSchema, Guid sessionId = default)
            : base(CreateParser(), new QueryExecutor(connector.Client, collectionSchema, sessionId, collectionName))
        {
            if (connector == null) throw new ArgumentNullException(nameof(connector));

            _client = connector.Client;

            _collectionName = collectionName ?? throw new ArgumentNullException(nameof(collectionName));

            _collectionSchema = collectionSchema ?? throw new ArgumentNullException(nameof(collectionSchema));
        }


        [UsedImplicitly]
        public DataSource(IQueryParser queryParser, IQueryExecutor executor)
            : base(new DefaultQueryProvider(typeof(DataSource<>), queryParser, executor))
        {
        }

        [UsedImplicitly]
        public DataSource(IQueryProvider provider, Expression expression)
            : base(provider, expression)
        {
        }


        /// <summary>
        ///     Get one item by primary key. Returns null if none
        /// </summary>
        /// <param name="primaryKey"></param>
        /// <returns></returns>
        public T this[object primaryKey]
        {
            get
            {
                var query = new QueryBuilder(_collectionSchema).GetOne(primaryKey);
                query.CollectionName = _collectionName;
                return _client.GetMany(query).Select(ri => ri.Item.ToObject<T>(SerializationHelper.Serializer))
                    .FirstOrDefault();
            }
        }

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


        /// <summary>
        ///     Only used in cache-only mode (no persistence). Declare a subset of the data as being fully loaded into the cache
        ///     Any expression that can be used for querying is valid here
        /// </summary>
        /// <param name="domainDefinition"></param>
        /// <param name="humanReadableDescription">Optional description of the loaded domain</param>
        public void DeclareLoadedDomain([NotNull] Expression<Func<T, bool>> domainDefinition,
            string humanReadableDescription = null)
        {
            if (domainDefinition == null) throw new ArgumentNullException(nameof(domainDefinition));


            var query = PredicateToQuery(domainDefinition);

            query.CollectionName = _collectionName;

            var domain = new DomainDescription(query, false, humanReadableDescription);

            _client.DeclareDomain(domain);
        }


        /// <summary>
        ///     Only used in cache-only mode (no persistence). Eviction is activated by collection.
        ///     Two types of eviction are supported for now: LRU (Less Recently Used) or TTL (Time To Live)
        ///     In LRU mode the
        ///     <param name="limit"></param>
        ///     and
        ///     <param name="itemsToRemove"></param>
        ///     are used
        ///     In TTL mode only
        ///     <param name="timeLimitInMilliseconds"></param>
        ///     is used
        ///     When the <paramref name="limit" /> is reached the less recently used  <paramref name="itemsToRemove" /> items are
        ///     evicted
        /// </summary>
        /// <param name="evictionType">LRU or TTL</param>
        /// <param name="limit">threshold that triggers the eviction in LRU mode</param>
        /// <param name="itemsToRemove">number of items to be evicted when the threshold is reached in LRU mode</param>
        /// <param name="timeLimitInMilliseconds">items older than the specified value are evicted in TTL mode</param>
        public void ConfigEviction(EvictionType evictionType, int limit, int itemsToRemove = 100,
            int timeLimitInMilliseconds = 0)
        {
            if (evictionType == EvictionType.LessRecentlyUsed && limit == 0)
                throw new ArgumentException("If LRU eviction is used, a positive limit must be specified");

            _client.ConfigEviction(_collectionName, evictionType, limit, itemsToRemove, timeLimitInMilliseconds);
        }


        /// <summary>
        ///     Mostly useful in distributed cache mode without persistence. Declare all instances of a given type as being
        ///     available
        ///     Usually called after the cache is fed. This will enable "get many" operations
        /// </summary>
        public void DeclareFullyLoaded(bool fullyLoaded = true)
        {
            var emptyQuery = new OrQuery(_collectionName);
            var domain = new DomainDescription(emptyQuery, fullyLoaded);

            _client.DeclareDomain(domain);
        }


        /// <summary>
        ///     Get items with SQL query
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public IEnumerable<T> SqlQuery(string sql)
        {
            var query = new Parser().ParseSql(sql).ToQuery(_collectionSchema);

            return _client.GetMany(query).Select(ri => QueryExecutor.FromJObject<T>(ri.Item));
        }


        /// <summary>
        ///     Update or insert an object
        /// </summary>
        /// <param name="item"></param>
        /// <param name="excludedFromEviction">In cache-only mode,if tue the item is never evicted from the cache</param>
        public void Put(T item, bool excludedFromEviction = false)
        {
            _client.Put(_collectionName, Pack(item), excludedFromEviction);
        }


        /// <summary>
        ///     Transactionally add a new item only if its not already present (primary key does not already exists)
        /// </summary>
        /// <param name="item"></param>
        /// <returns>true if added, false if already present</returns>
        public bool TryAdd(T item)
        {
            return _client.TryAdd(_collectionName, Pack(item));
        }

        /// <summary>
        ///     Delete one item by primary key
        /// </summary>
        /// <param name="item"></param>
        public void Delete(T item)
        {
            var packed = Pack(item);
            var query = new QueryBuilder(_collectionSchema).GetOne(packed.PrimaryKey);
            query.CollectionName = _collectionName;

            _client.RemoveMany(query);
        }


        /// <summary>
        ///     Remove all the items matching the query
        ///     This method is transactional on a single node cluster
        /// </summary>
        /// <param name="where"></param>
        public void DeleteMany(Expression<Func<T, bool>> where)
        {
            var query = PredicateToQuery(where);
            query.CollectionName = _collectionName;

            _client.RemoveMany(query);
        }


        /// <summary>
        ///     Creates a pivot definition. It will be enriched with axis list and properties to be aggregated
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public PivotDefinition<T> PreparePivotRequest(Expression<Func<T, bool>> filter = null)
        {
            var query = filter != null ? PredicateToQuery(filter) : new OrQuery(_collectionName);

            return new PivotDefinition<T>(query, _client, _collectionSchema);
        }


        /// <summary>
        ///     Precompile an expression tree as a query. It can be used to avoid parsing the expression tree multiple times
        /// </summary>
        /// <param name="where"></param>
        /// <param name="collectionName"></param>
        /// <returns></returns>
        public OrQuery PredicateToQuery(Expression<Func<T, bool>> where, string collectionName = null)
        {
            collectionName ??= _collectionSchema.CollectionName;

            // create a fake queryable to force query parsing and capture resolution
            var executor = new NullExecutor(_collectionSchema, collectionName);
            var queryable = new NullQueryable<T>(executor);

            var unused = queryable.Where(where).ToList();

            var query = executor.Expression;
            query.CollectionName = _collectionName;

            return query;
        }

        /// <summary>
        ///     Get many with a precompiled query. Useful hen the same query is used multiple times
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public IEnumerable<T> WithPrecompiledQuery(OrQuery query)
        {
            return _client.GetMany(query).Select(ri => QueryExecutor.FromJObject<T>(ri.Item));
        }

        /// <summary>
        ///     Conditional update. The item is updated only if the predicate evaluated server-site against the PREVIOUS value is
        ///     true. The test and update are processed as an atomic operation.
        ///     Can be used in "optimistic synchronization" scenarios.
        ///     Throws an exception if the condition is not satisfied
        /// </summary>
        /// <param name="newValue"></param>
        /// <param name="test"></param>
        public void UpdateIf(T newValue, Expression<Func<T, bool>> test)
        {
            var testAsQuery = PredicateToQuery(test);

            _client.UpdateIf(Pack(newValue), testAsQuery);
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


            var kv = new KeyValue(oldTimestamp);

            
            var q = new AtomicQuery(_collectionSchema.KeyByName("Timestamp"), kv);
            var andQuery = new AndQuery();
            andQuery.Elements.Add(q);
            var orq = new OrQuery(_collectionName);
            orq.Elements.Add(andQuery);

            var now = DateTime.Now;
            var newTimestamp = now.AddTicks(1); // add one to be sure its different


            prop.SetValue(newValue, newTimestamp);

            _client.UpdateIf(Pack(newValue), orq);
        }


        /// <summary>
        ///     Update or insert a collection of objects. Items are fed by package to optimize network transport.
        ///     For many items an optimized bulk insert algorithm is used
        ///     This method is transactional on a single node cluster
        /// </summary>
        /// <param name="items"></param>
        public void PutMany(IEnumerable<T> items)
        {
            _client.FeedMany(_collectionName, items.Select(Pack), false, 10000);
        }
    }
}