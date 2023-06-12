using System;
using System.Collections.Generic;
using System.Linq;
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


    /// <summary>
    ///     Internally used to select the most efficient indexes for a query
    /// </summary>
    private class IndexRanking
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


    public QueryManager(DataStore dataStore, ILog log = null)
    {
        _dataStore = dataStore;
        _log = log;
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
                // for primary or unique key we do not need more than one index 
                if (index.IndexType == IndexType.Primary)
                    // no need to count for the primary index. Waste of time as it wil always be the only index used
                    return new List<IndexRanking> { new(index, atomicQuery, -1) };

                var indexResultCount = index.GetCount(atomicQuery.Values, atomicQuery.Operator);
                result.Add(new(index, atomicQuery, indexResultCount));
            }
            else if (atomicQuery.IsComparison) // in this case we can only use ordered indexes
            {
                if (index.IndexType == IndexType.Ordered)
                {
                    var indexResultCount = index.GetCount(atomicQuery.Values, atomicQuery.Operator, true);

                    result.Add(new(index, atomicQuery, indexResultCount));
                }
            }
        }

        return result;
    }

    private List<PackedObject> ProcessAndQuery(AndQuery query, QueryExecutionPlan queryExecutionPlan,
                                               OrQuery parentQuery)
    {
        queryExecutionPlan.StartPlanning();
        var indexesThatCanBeUsed = GetIndexesForQuery(query);
        var indexesUsed = indexesThatCanBeUsed.OrderBy(p => p.Ranking).Take(2).ToArray();
        // remove the second index if it matches much more items than the first one
        if (indexesUsed.Length > 1)
            if (indexesUsed[1].Ranking > indexesUsed[0].Ranking * 4)
                indexesUsed = new[] { indexesUsed[0] };

        queryExecutionPlan.EndPlanning(indexesUsed.Select(r => r.Index.Name).ToList());

        // this will contain all queries that have can not be resolved by indexes and need to be checked manually 
        var restOfTheQuery = query.Clone();

        ISet<PackedObject> result = null;

        var finalResult = new List<PackedObject>();


        if (indexesUsed.Length == 1) // only one index can be used so do not bother with extra logic
        {
            queryExecutionPlan.StartIndexUse();
            var plan = indexesUsed[0];

            queryExecutionPlan.Trace($"single index: {plan.ResolvedQuery.PropertyName}");

            result = plan.Index.GetMany(plan.ResolvedQuery.Values, plan.ResolvedQuery.Operator);

            // this query was resolved by an index so no need to check it manually
            restOfTheQuery.Elements.Remove(plan.ResolvedQuery);

            queryExecutionPlan.EndIndexUse();
        }
        else if (indexesUsed.Length > 1)
        {
            queryExecutionPlan.StartIndexUse();

            foreach (var plan in indexesUsed) // no more than two indexes
            {
                if (result == null)
                {
                    result = plan.Index.GetMany(plan.ResolvedQuery.Values, plan.ResolvedQuery.Operator);
                    queryExecutionPlan.Trace($"first index: {plan.ResolvedQuery.PropertyName} = {plan.Ranking}");
                }
                else
                {
                    result.IntersectWith(plan.Index.GetMany(plan.ResolvedQuery.Values, plan.ResolvedQuery.Operator));
                    queryExecutionPlan.Trace(
                        $"then index: {plan.ResolvedQuery.PropertyName} = {plan.Ranking} => {result.Count}");
                }

                // do not work too hard if indexes found nothing
                if (result.Count == 0) break;

                // this query was resolved by an index so no need to check it manually
                restOfTheQuery.Elements.Remove(plan.ResolvedQuery);
            }

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

            foreach (var item in result)
                if (restOfTheQuery.Match(item))
                    finalResult.Add(item);

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
            ExecutionPlan = ExecutionPlan;

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
            foreach (var o in index.GetAll(descending))
                if (set.Contains(o))
                    result.Add(o);

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

        if (atomicQuery.Operator is QueryOperator.Eq or QueryOperator.In)
            if (atomicQuery.IndexType == IndexType.Primary)
            {
                queryExecutionPlan.UsedIndexes = new() { _dataStore.PrimaryIndex.Name };
                return _dataStore.PrimaryIndex.GetMany(atomicQuery.Values).ToList();
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
                    return index.GetMany(atomicQuery.Values).ToList();
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
                    return index.GetMany(atomicQuery.Values, atomicQuery.Operator).ToList();
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
                    return index.GetMany(atomicQuery.Values, atomicQuery.Operator).ToList();
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
        List<PackedObject> result = orQuery.OrderByProperty == null
            ? _dataStore.PrimaryIndex.GetAll().Where(query.Match).ToList()
            : null;

        // otherwise use an ordered index
        if (result == null)
        {
            var index = _dataStore.TryGetIndex(orQuery.OrderByProperty);
            if (index is not OrderedIndex)
                throw new CacheException(
                    $"Order by can be applied only on ordered indexes, {orQuery.OrderByProperty} is not one");

            result = index.GetAll(orQuery.OrderByIsDescending).Where(query.Match).ToList();
        }

        // this will apply DISTINCT and TAKE (if required)
        return PostProcessResult(orQuery, result);
    }


    public ExecutionPlan ExecutionPlan { get; private set; }

    private void ProcessGetRequest(GetRequest request, IClient client)
    {
        try
        {
            var query = request.Query;

            var indexOfSelectedProperties = _dataStore.CollectionSchema.IndexesOfNames(query.SelectClause
                .Select(s => s.Name)
                .ToArray());

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
            foreach (var o in ftResult)
                if (queryResult.Contains(o))
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
    ///     Apply distinct and take operators.
    /// </summary>
    /// <param name="query"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    private IList<PackedObject> PostProcessResult(OrQuery query, IList<PackedObject> result)
    {
        if (query.Distinct)
        {
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
}