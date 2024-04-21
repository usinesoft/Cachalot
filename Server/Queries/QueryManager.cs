using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Client.ChannelInterface;
using Client.Core;
using Client.Interface;
using Client.Messages;
using Client.Messages.Pivot;
using Client.Queries;


namespace Server.Queries;

/// <summary>
///     Manages a read-only DataRequest
///     A new instance is created for each request
/// </summary>
public class QueryManager : IRequestManager
{
    /// <summary>
    ///     Query execution plan with timing for each stage
    /// </summary>
    private readonly DataStore _dataStore;

    private readonly ILog _log;


    public QueryManager(DataStore dataStore, ILog log = null)
    {
        _dataStore = dataStore;
        _log = log;
    }


    public ExecutionPlan ExecutionPlan { get; private set; }

    public void ProcessRequest(Request request, IClient client)
    {
        switch (request)
        {
            case GetRequest getRequest:
                ProcessGetRequest(getRequest, client);
                break;
            case EvalRequest evalRequest:
                ProcessEvalRequest(evalRequest, client);
                break;

            case PivotRequest pivotRequest:
                ProcessPivotRequest(pivotRequest, client);
                break;
        }
    }


    /// <summary>
    ///     Rank the indexes that can be used to resolve the query. Lower rank means more discriminant index (smaller result)
    /// </summary>
    /// <param name="andQuery"></param>
    /// <returns></returns>
    private IList<IndexRanking> GetIndexesForQuery(AndQuery andQuery)
    {
        var result = new List<IndexRanking>();

        foreach (var atomicQuery in andQuery.Elements)
        {
            var name = atomicQuery.PropertyName;

            var index = _dataStore.TryGetIndex(name);

            if (index == null)
                continue;

            if (atomicQuery.Operator is QueryOperator.Eq or QueryOperator.In or QueryOperator.Contains)
            {
                // For primary or unique key we do not need more than one index. 
                // No need to count for the primary index. Waste of time as it wil always be the only index used
                if (index.IndexType == IndexType.Primary)
                    return new List<IndexRanking> { new(index, atomicQuery, -1) };

                var indexResultCount = index.GetCount(atomicQuery.GetValues(), atomicQuery.Operator);
                result.Add(new(index, atomicQuery, indexResultCount));
            }
            else if (atomicQuery.IsComparison) // in this case we can only use ordered indexes
            {
                if (index.IndexType != IndexType.Ordered) continue;

                var indexResultCount = index.GetCount(atomicQuery.GetValues(), atomicQuery.Operator, true);

                result.Add(new(index, atomicQuery, indexResultCount));
            }
        }

        return result;
    }

    private List<PackedObject> ProcessAndQuery(AndQuery query, QueryExecutionPlan queryExecutionPlan,
                                               OrQuery parentQuery)
    {
        queryExecutionPlan.StartPlanning();
        var indexesThatCanBeUsed = GetIndexesForQuery(query);

        // A difficult decision after many tests. Using more than one index is almost always slower than 
        // using only the most efficient index and scan the result to check for the rest of the conditions.
        // This is probably true only for in-memory databases
        var indexesUsed = indexesThatCanBeUsed.MinBy(p => p.Ranking);

        var usedIndexes = new List<string>();
        if (indexesUsed != null) usedIndexes.Add(indexesUsed.Index.Name);

        queryExecutionPlan.EndPlanning(usedIndexes);

        // this will contain all queries that have can not be resolved by indexes and need to be checked manually 
        var restOfTheQuery = query.Clone();

        ISet<PackedObject> result;

        var finalResult = new List<PackedObject>();


        if (indexesUsed != null) // only one index can be used so do not bother with extra logic
        {
            queryExecutionPlan.StartIndexUse();
            var plan = indexesUsed;

            queryExecutionPlan.Trace($"single index: {plan.ResolvedQuery.PropertyName}");

            result = plan.Index.GetMany(plan.ResolvedQuery.GetValues(), plan.ResolvedQuery.Operator);

            // this query was resolved by an index so no need to check it manually
            restOfTheQuery.Elements.Remove(plan.ResolvedQuery);

            queryExecutionPlan.EndIndexUse();
        }
        else // no index can be used so proceed to full-scan
        {
            queryExecutionPlan.FullScan = true;

            queryExecutionPlan.StartScan();


            var res = SmartFullScan(parentQuery).ToList();

            queryExecutionPlan.EndScan();

            return res;
        }


        if (result != null)
        {
            if (restOfTheQuery.Elements.Count == 0) // empty query left; fully resolved by indexes
                return result.ToList();

            queryExecutionPlan.StartScan();

            finalResult.AddRange(result.Where(restOfTheQuery.Match));

            queryExecutionPlan.EndScan();
        }


        return finalResult;
    }

    private AtomicQuery AsAtomic(OrQuery query)
    {
        if (query.Elements.Count != 1)
            return null;

        if (query.Elements[0].Elements.Count != 1)
            return null;

        return query.Elements[0].Elements[0];
    }


    private IList<PackedObject> InternalProcessQuery(OrQuery query)
    {
        ExecutionPlan = new();

        try
        {
            ExecutionPlan.Begin();

            // an empty query should return everything
            if (query.IsEmpty())
            {
                ExecutionPlan.QueryPlans.Add(new(query.ToString()));

                // special processing for distinct clause on a single column
                if (query.Distinct && query.SelectClause.Count == 1)
                    ExecutionPlan.SimpleDistinct = true;

                return SmartFullScan(query)
                    .ToList();
            }

            // simplified processing if it is an atomic query
            var atomicQuery = AsAtomic(query);

            if (atomicQuery != null)
            {
                ExecutionPlan.QueryPlans.Add(new(query.ToString()));

                var res = ProcessSimpleQuery(atomicQuery, query);

                // full-scan queries are already ordered
                if (query.OrderByProperty != null && !ExecutionPlan.QueryPlans[0].FullScan)
                    return OrderBy(res, query.OrderByProperty, query.OrderByIsDescending);
                else
                    return res;
            }


            // if only one AndQuery, process sequentially
            if (query.Elements.Count == 1)
            {
                var andQuery = query.Elements[0];

                ExecutionPlan.QueryPlans.Add(new(query.ToString()));

                var result = ProcessAndQuery(andQuery, ExecutionPlan.QueryPlans[0], query);

                // full-scan queries are already ordered
                if (query.OrderByProperty != null && !ExecutionPlan.QueryPlans[0].FullScan)
                    return OrderBy(result, query.OrderByProperty, query.OrderByIsDescending);

                return result;
            }

            // if multiple AndQueries run in parallel
            HashSet<PackedObject> orResult = null;

            var results = new List<PackedObject>[query.Elements.Count];

            foreach (var andQuery in query.Elements) ExecutionPlan.QueryPlans.Add(new(andQuery.ToString()));

            Parallel.For(0, query.Elements.Count, i =>
            {
                var andQuery = query.Elements[i];
                results[i] = ProcessAndQuery(andQuery, ExecutionPlan.QueryPlans[i], query);
            });

            ExecutionPlan.BeginMerge();

            // merge the results (they may contain duplicates)
            foreach (var result in results)
                if (orResult == null)
                    orResult = new(result);
                else
                    orResult.UnionWith(result);

            ExecutionPlan.EndMerge();

            if (query.OrderByProperty != null)
                return OrderBy(orResult!.ToList(), query.OrderByProperty, query.OrderByIsDescending);


            return orResult!.ToList();
        }
        finally
        {
            ExecutionPlan.End();

            if (!query.CollectionName.Equals(LogEntry.Table,
                    StringComparison.InvariantCultureIgnoreCase)) // do not log queries on @ACTIVITY table itself
            {
                var type = query.CountOnly ? LogEntry.Eval : LogEntry.Select;
                _log?.LogActivity(type, query.CollectionName, ExecutionPlan.TotalTimeInMicroseconds, query.ToString(),
                    query.Description(), ExecutionPlan, query.QueryId);
            }
        }
    }

    private List<PackedObject> OrderBy(List<PackedObject> selectedItems, string orderByProperty,
                                       in bool orderByIsDescending)
    {
        ExecutionPlan.BeginOrderBy();

        var allCount = _dataStore.DataByPrimaryKey.Count;
        var selectedCount = selectedItems.Count;

        // for small subsets it is faster to sort without scanning the index
        var doNotUseIndex = selectedCount * Math.Log2(selectedCount) < allCount;


        var result = doNotUseIndex
            ? OrderByWithoutIndex(selectedItems, orderByProperty, orderByIsDescending)
            : OrderByWithIndex(selectedItems, orderByProperty, orderByIsDescending);


        ExecutionPlan.EndOrderBy();

        return result;
    }

    private List<PackedObject> OrderByWithoutIndex(List<PackedObject> items, string orderByProperty, bool descending)
    {
        var property = _dataStore.CollectionSchema.KeyByName(orderByProperty);
        if (property == null) throw new CacheException($"The property {orderByProperty} not found");

        var index = property.Order;

        items.Sort((x, y) =>
        {
            var xv = x.Values[index];
            var yv = y.Values[index];
            var result = xv.CompareTo(yv);
            return descending ? -result : result;
        });

        return items;
    }

    private List<PackedObject> OrderByWithIndex(List<PackedObject> items, string orderByProperty, bool descending)
    {
        var set = new HashSet<PackedObject>(items);

        var result = new List<PackedObject>(set.Count);

        var index = _dataStore.TryGetIndex(orderByProperty);

        if (index.IndexType == IndexType.Ordered)
        {
            // sort with an ordered index: iterate through the whole index and retain only objects that match the query
            result.AddRange(index.GetAll(descending).Where(set.Contains));

            return result;
        }

        throw new CacheException("Order by can be used only on an ordered index");
    }

    private IList<PackedObject> Distinct(IEnumerable<PackedObject> input, ExecutionPlan executionPlan,
                                         params int[] indexes)
    {
        executionPlan.BeginDistinct();

        var result = new List<PackedObject>();
        var distinct = new HashSet<Projection>();
        foreach (var o in input)
        {
            var projection = new Projection(o, indexes);
            if (distinct.Add(projection)) result.Add(o);
        }

        executionPlan.EndDistinct();

        return result;
    }


    /// <summary>
    ///     Faster processing for simple query (that contains a single test).
    /// </summary>
    /// <param name="atomicQuery"></param>
    /// <param name="parentQuery"></param>
    /// <returns></returns>
    private List<PackedObject> ProcessSimpleQuery(AtomicQuery atomicQuery, OrQuery parentQuery)
    {
        if (ExecutionPlan.QueryPlans.Count == 0) ExecutionPlan.QueryPlans.Add(new(atomicQuery.ToString()));

        var queryExecutionPlan = ExecutionPlan.QueryPlans[0];
        queryExecutionPlan.SimpleQueryStrategy = true;

        if (atomicQuery.Operator is QueryOperator.Eq or QueryOperator.In && atomicQuery.IndexType == IndexType.Primary)
        {
            queryExecutionPlan.UsedIndexes = new() { _dataStore.PrimaryIndex.Name };
            return _dataStore.PrimaryIndex.GetMany(atomicQuery.GetValues()).ToList();
        }


        var index = _dataStore.TryGetIndex(atomicQuery.PropertyName);

        if (index != null)
        {
            if (atomicQuery.Operator == QueryOperator.Eq) // works with all kinds of indexes
                try
                {
                    queryExecutionPlan.Trace($"single index: {atomicQuery.PropertyName}");
                    queryExecutionPlan.StartIndexUse();
                    queryExecutionPlan.UsedIndexes = new() { index.Name };
                    return index.GetMany(atomicQuery.GetValues()).ToList();
                }
                finally
                {
                    queryExecutionPlan.EndIndexUse();
                }

            if (index.IndexType == IndexType.Ordered && atomicQuery.IsComparison)
                try
                {
                    queryExecutionPlan.Trace($"single index: {atomicQuery.PropertyName}");
                    queryExecutionPlan.StartIndexUse();
                    queryExecutionPlan.UsedIndexes = new() { index.Name };
                    return index.GetMany(atomicQuery.GetValues(), atomicQuery.Operator).ToList();
                }
                finally
                {
                    queryExecutionPlan.EndIndexUse();
                }

            if (atomicQuery.Operator is QueryOperator.In or QueryOperator.Contains)
                try
                {
                    queryExecutionPlan.Trace($"single index: {atomicQuery.PropertyName}");
                    queryExecutionPlan.StartIndexUse();
                    queryExecutionPlan.UsedIndexes = new() { index.Name };
                    return index.GetMany(atomicQuery.GetValues(), atomicQuery.Operator).ToList();
                }
                finally
                {
                    queryExecutionPlan.EndIndexUse();
                }
        }

        // if we reached this point the only strategy left is full-scan
        queryExecutionPlan.FullScan = true;
        try
        {
            queryExecutionPlan.StartScan();

            // manage special case for ordered queries
            return SmartFullScan(parentQuery).ToList();
        }
        finally
        {
            queryExecutionPlan.EndScan();
        }
    }

    /// <summary>
    ///     If the result must be ordered use an ordered index, otherwise use the primary index
    /// </summary>
    /// <returns></returns>
    /// <exception cref="CacheException"></exception>
    private IEnumerable<PackedObject> SmartFullScan(OrQuery orQuery)
    {
        ExecutionPlan.MatchedItems = _dataStore.DataByPrimaryKey.Count;

        var atomicQuery = AsAtomic(orQuery);


        var query = (Query)atomicQuery ?? orQuery; // matching with atomic query is slightly faster

        // if not ORDER BY use the primary index
        var result = orQuery.OrderByProperty == null
            ? _dataStore.PrimaryIndex.GetAll()
            : null;

        // otherwise use an ordered index
        if (result == null)
        {
            var index = _dataStore.TryGetIndex(orQuery.OrderByProperty);
            if (index is not OrderedIndex)
                throw new CacheException(
                    $"Order by can be applied only on ordered indexes, {orQuery.OrderByProperty} is not one");

            result = index.GetAll(orQuery.OrderByIsDescending);
        }

        if (!query.IsEmpty()) result = result.Where(query.Match);

        // no post-processing required
        if (!orQuery.Distinct && orQuery.Take == 0) return result;

        if (!orQuery.Distinct) return result.Take(orQuery.Take);

        // this will apply DISTINCT and TAKE 
        return PostProcessResult(orQuery, result.ToList());
    }

    private void ProcessGetRequest(GetRequest request, IClient client)
    {
        try
        {
            var query = request.Query;

            var indexOfSelectedProperties = _dataStore.CollectionSchema.IndexesOfNames(query.SelectClause
                .Select(s => s.Name)
                .ToArray());

            // faster processing for simple distinct queries
            if (query.IsEmpty() && query.Distinct && query.SelectClause.Count == 1)
            {
                var propertyName = query.SelectClause[0].Name;
                var values = SimpleDistinct(propertyName, query.Take);

                //pack values as Json objects
                var valuesList = new List<JsonDocument>(values.Count);
                foreach (var value in values)
                {
                    var jo = new JsonObject( new []{ new KeyValuePair<string, JsonNode>(propertyName, value.ToJsonValue())});
                    valuesList.Add(jo.Deserialize<JsonDocument>());
                }


                client.SendMany(valuesList);

                return;
            }

            var result = ProcessQuery(query);

            var aliases = query.SelectClause.Select(s => s.Alias).ToArray();


            if (query.SelectClause.Count > 0)
            {
                client.SendMany(result, indexOfSelectedProperties, aliases);
            }
            else
            {
                // for flat layout we have to generate dynamically the JSON
                if (result.Count > 0 && result[0].Layout == Layout.Flat)
                {
                    var names = _dataStore.CollectionSchema.ServerSide.Select(x => x.Name).ToArray();
                    var indexes = Enumerable.Range(0, names.Length).ToArray();

                    client.SendMany(result, indexes, names);
                }
                else
                {
                    client.SendMany(result, Array.Empty<int>(), null);
                }
            }
        }
        catch (Exception e)
        {
            client.SendResponse(new ExceptionResponse(e));
        }
        finally
        {
            _dataStore.ProcessEviction();
        }
    }

    public IList<PackedObject> ProcessQuery(OrQuery query)
    {
        if (query.OnlyIfComplete)
        {
            var domainDescription = _dataStore.DomainDescription;
            var dataIsComplete = domainDescription is { IsFullyLoaded: true };

            if (!dataIsComplete && domainDescription != null && !domainDescription.DescriptionAsQuery.IsEmpty())
                dataIsComplete = query.IsSubsetOf(domainDescription.DescriptionAsQuery);


            if (!dataIsComplete)
                throw new CacheException("Full data is not available for type " +
                                         _dataStore.CollectionSchema.CollectionName);
        }

        IList<PackedObject> result = new List<PackedObject>();


        // Pure full-text search
        if (query.IsFullTextQuery && query.IsEmpty())
        {
            result = ProcessFullTextQuery(query.FullTextSearch, query.Take);
        }
        // Mixed search : the result wil be the intersection of the full-text result and the query result
        // The ordered by clause is ignored if present. The order is given by the full text search pertinence
        else if (query.IsFullTextQuery)
        {
            ISet<PackedObject> queryResult = new HashSet<PackedObject>();
            IList<PackedObject> ftResult = new List<PackedObject>();

            query.OrderByProperty = null;
            query.OrderByIsDescending = false;

            // run the structured query and the full-text one in parallel
            Parallel.Invoke(
                () => { queryResult = InternalProcessQuery(query).ToHashSet(); },
                () => { ftResult = ProcessFullTextQuery(query.FullTextSearch, query.Take); });

            // the order will be the one in the full-text result (it is ranked by pertinence)
            foreach (var o in ftResult.Where(queryResult.Contains))
                result.Add(o);
        }
        // No full-text, structured query
        else
        {
            result = InternalProcessQuery(query);
        }

        result = PostProcessResult(query, result);

        return result;
    }

    /// <summary>
    ///     Used for distinct queries without where clause and having single column in select clause
    ///     They can be processed much faster than the generic case
    /// </summary>
    /// <param name="column"></param>
    /// <param name="take">if greater than 0 limit the length of the result </param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    private IList<KeyValue> SimpleDistinct(string column, int take)
    {
        if (take == 0) take = int.MaxValue;

        var metadata = _dataStore.CollectionSchema.ServerSide
            .Find(x => x.Name.Equals(column, StringComparison.InvariantCultureIgnoreCase));

        if (metadata == null)
            throw new ArgumentException($"Unknown property {column}");

        var unique = new HashSet<KeyValue>();

        // if a dictionary index is available use it
        var index = _dataStore.TryGetIndex(column);

        if (index is { IndexType: IndexType.Dictionary })
        {
            var dictionary = index as DictionaryIndex;
            return dictionary!.Keys.Take(take).ToList();
        }

        // otherwise proceed to full scan
        if (metadata.IsCollection)
            foreach (var packedObject in _dataStore.DataByPrimaryKey.Values)
            {
                var values = packedObject.CollectionValues[metadata.Order];
                foreach (var keyValue in values.Values) unique.Add(keyValue);
            }
        else // scalar value
            foreach (var packedObject in _dataStore.DataByPrimaryKey.Values)
            {
                var value = packedObject.Values[metadata.Order];
                unique.Add(value);
            }

        return unique.Take(take).ToList();
    }

    /// <summary>
    ///     Apply distinct and take operators.
    /// </summary>
    /// <param name="query"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    private IList<PackedObject> PostProcessResult(OrQuery query, IList<PackedObject> result)
    {
        if (query.Distinct && !ExecutionPlan.SimpleDistinct) // simple distinct already processed
        {
            if (query.SelectClause.Count == 0) throw new NotSupportedException("DISTINCT clause requires a projection");

            var indexOfSelectedProperties = _dataStore.CollectionSchema.IndexesOfNames(query.SelectClause
                .Select(s => s.Name)
                .ToArray());

            result = Distinct(result, ExecutionPlan, indexOfSelectedProperties);
        }


        // spent some time on this choice. Touch is used for the eviction (in LRU mode). In this case queries  
        // usually return one or zero items as data is not completely available (used as simple cache) 
        if (result.Count == 1) _dataStore.Touch(result[0]);


        _dataStore.IncrementReadCount();


        // also used only in cache mode
        if (result.Count > 0) _dataStore.IncrementHitCount();

        // here it may not be initialized if pure full-text query
        ExecutionPlan ??= new();

        ExecutionPlan.MatchedItems = result.Count;

        // limit the result if needed
        if (query.Take != 0 && query.Take < result.Count) result = result.Take(query.Take).ToList();


        return result;
    }

    private void ProcessPivotRequest(PivotRequest pivotRequest, IClient client)
    {
        try
        {
            var filtered = ProcessQuery(pivotRequest.Query);

            var pr = new PivotResponse();

            // partition data to allow for parallel calculation
            var groupBy = filtered.GroupBy(i => i.PrimaryKey.GetHashCode() % 10);


            Parallel.ForEach(groupBy, new() { MaxDegreeOfParallelism = 10 },
                objects =>
                {
                    var pivot = new PivotLevel(_dataStore.CollectionSchema, pivotRequest.AxisList,
                        pivotRequest.ValuesList);

                    foreach (var o in objects) pivot.AggregateOneObject(o);

                    lock (pr)
                    {
                        pr.Root.MergeWith(pivot);
                    }
                });


            client.SendResponse(pr);
        }
        catch (Exception e)
        {
            client.SendResponse(new ExceptionResponse(e));
        }
    }

    private void ProcessEvalRequest(EvalRequest evalRequest, IClient client)
    {
        try
        {
            var result = ProcessQuery(evalRequest.Query);

            var count = evalRequest.Query.Take > 0 ? Math.Min(result.Count, evalRequest.Query.Take) : result.Count;
            var response = new EvalResponse
            {
                Items = count,
                Complete = _dataStore.DomainDescription != null &&
                           evalRequest.Query.IsSubsetOf(_dataStore.DomainDescription.DescriptionAsQuery)
            };

            client.SendResponse(response);
        }
        catch (Exception e)
        {
            client.SendResponse(new ExceptionResponse(e));
        }
    }


    private IList<PackedObject> ProcessFullTextQuery(string query, int take)
    {
        return _dataStore.FullTextSearch(query, take);
    }


    /// <summary>
    ///     Internally used to select the most efficient indexes for a query
    /// </summary>
    private sealed class IndexRanking
    {
        public IndexRanking(IReadOnlyIndex index, AtomicQuery resolvedQuery, int ranking)
        {
            Index = index;
            ResolvedQuery = resolvedQuery;
            Ranking = ranking;
        }

        public IReadOnlyIndex Index { get; }

        public AtomicQuery ResolvedQuery { get; }

        /// <summary>
        ///     Number of objects resolved by this index. Lower is better (more discriminant index)
        /// </summary>
        public int Ranking { get; }
    }
}