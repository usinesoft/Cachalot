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

namespace Server.Queries
{
    /// <summary>
    /// Manages a read-only DataRequest
    /// A new instance is created for each request
    /// </summary>
    public class QueryManager:IRequestManager
    {
        /// <summary>
        /// Query execution plan with timing for each stage
        /// </summary>
        

        private readonly DataStore _dataStore;

        private readonly ILog _log;
        
        
        /// <summary>
        /// Internally used to select the most efficient indexes for a query
        /// </summary>
        class IndexRanking
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
            /// Number of objects resolved by this index. Lower is better (more discriminant index)
            /// </summary>
            public int Ranking { get; }

        }

        
        public QueryManager(DataStore dataStore, ILog log = null)
        {
            _dataStore = dataStore;
            _log = log;

            
        }


        /// <summary>
        /// Rank the indexes that can be used to resolve the query. Lower rank means more discriminant index (smaller result)
        /// </summary>
        /// <param name="andQuery"></param>
        /// <returns></returns>
        IList<IndexRanking> GetIndexesForQuery(AndQuery andQuery)
        {
            var result = new List<IndexRanking>();

            foreach (var atomicQuery in andQuery.Elements)
            {
                var name = atomicQuery.PropertyName;
                
                var index = _dataStore.TryGetIndex(name);

                if(index == null)
                    continue;

                if (atomicQuery.Operator == QueryOperator.Eq || atomicQuery.Operator == QueryOperator.In || atomicQuery.Operator == QueryOperator.Contains)
                {
                    
                    // for primary or unique key we do not need more than one index 
                    if (index.IndexType == IndexType.Primary)
                    {
                        
                        // no need to count for the primary index. Waste of time as it wil always be the only index used
                        return new List<IndexRanking>{new IndexRanking(index, atomicQuery, -1)};
                    }

                    var indexResultCount = index.GetCount(atomicQuery.Values, atomicQuery.Operator);
                    result.Add(new IndexRanking(index, atomicQuery, indexResultCount));

                }
                else if(atomicQuery.IsComparison) // in this case we can only use ordered indexes
                {
                    if (index.IndexType == IndexType.Ordered)
                    {
                        var indexResultCount = index.GetCount(atomicQuery.Values, atomicQuery.Operator);

                        result.Add(new IndexRanking(index, atomicQuery, indexResultCount));
                    }
                    
                }

            }

            return result;
        }

        IList<PackedObject> ProcessAndQuery(AndQuery query, ExecutionPlan executionPlan)
        {

            if (query.Elements.Count == 1)
            {
                return ProcessSimpleQuery(query.Elements[0], executionPlan);
            }

            var queryExecutionPlan = new QueryExecutionPlan(query.ToString());
            
            // this method can be called in parallel. The only common data is the global execution plan
            lock (executionPlan)
            {
                executionPlan.QueryPlans.Add(queryExecutionPlan);    
            }
            

            queryExecutionPlan.StartPlanning();
            var indexesToUse = GetIndexesForQuery(query);
            queryExecutionPlan.EndPlanning(indexesToUse.Select(r=>r.Index.Name).ToList());

            // this will contain all queries that have can not be resolved by indexes and need to be checked manually 
            var restOfTheQuery = query.Clone();

            ISet<PackedObject> result = null;

            var finalResult = new List<PackedObject>();

            

            if (indexesToUse.Count == 1) // only one index can be used so do not bother with extra logic
            {
                queryExecutionPlan.StartIndexUse();
                var plan = indexesToUse[0];

                queryExecutionPlan.Trace($"single index: {plan.ResolvedQuery.PropertyName}");

                result = plan.Index.GetMany(plan.ResolvedQuery.Values, plan.ResolvedQuery.Operator);

                // this query was resolved by an index so no need to check it manually
                restOfTheQuery.Elements.Remove(plan.ResolvedQuery);

                queryExecutionPlan.EndIndexUse();
            }
            else if (indexesToUse.Count > 1)
            {
                queryExecutionPlan.StartIndexUse();

                foreach (var plan in indexesToUse.OrderBy(p=>p.Ranking).Take(2)) // no more than two indexes
                {
                    if (result == null)
                    {
                        result = plan.Index.GetMany(plan.ResolvedQuery.Values, plan.ResolvedQuery.Operator);
                        queryExecutionPlan.Trace($"first index: {plan.ResolvedQuery.PropertyName} = {plan.Ranking}");
                    }
                    else
                    {
                        result.IntersectWith(plan.Index.GetMany(plan.ResolvedQuery.Values, plan.ResolvedQuery.Operator));
                        queryExecutionPlan.Trace($"then index: {plan.ResolvedQuery.PropertyName} = {plan.Ranking} => {result.Count}");
                    }

                    // do not work too hard if indexes found nothing
                    if (result.Count == 0)
                    {
                        break;
                    }

                    // this query was resolved by an index so no need to check it manually
                    restOfTheQuery.Elements.Remove(plan.ResolvedQuery);
                }

                queryExecutionPlan.EndIndexUse();
            }
            else // no index can be used so proceed to full-scan
            {
                queryExecutionPlan.FullScan = true;

                queryExecutionPlan.StartScan();
                var res =  _dataStore.PrimaryIndex.GetAll().Where(o=>restOfTheQuery.Match(o)).ToList();
                queryExecutionPlan.EndScan();

                return res;
            }


            if (result != null)
            {
                if (restOfTheQuery.Elements.Count == 0) // empty query left; fully resolved by indexes
                {
                    return result.ToList();
                }

                queryExecutionPlan.StartScan();

                foreach (var item in result)
                {
                    if (restOfTheQuery.Match(item))
                        finalResult.Add(item);
                }

                queryExecutionPlan.EndScan();
            }
                

            return finalResult;
        }

        AtomicQuery AsAtomic(OrQuery query)
        {
            if (query.Elements.Count != 1)
                return null;

            if (query.Elements[0].Elements.Count != 1)
                return null;

            return query.Elements[0].Elements[0];

        }


        private IList<PackedObject> InternalProcessQuery(OrQuery query)
        {
            var executionPlan = new ExecutionPlan();

            try
            {
                
                executionPlan.Begin();

                // an empty query should return everything
                if (query.IsEmpty())
                {
                    var all =  _dataStore.PrimaryIndex.GetAll().ToList();

                    
                    if (query.OrderByProperty != null)
                    {
                        return OrderBy(all.ToHashSet(), query.OrderByProperty, query.OrderByIsDescending, executionPlan);
                    }
                

                    return all;
                } 

                // simplified processing if it is an atomic query
                var atomicQuery = AsAtomic(query);

                if (atomicQuery != null)
                {
                    
                    var res = ProcessSimpleQuery(atomicQuery, executionPlan);

                   
                    if (query.OrderByProperty != null)
                    {
                        return OrderBy(res.ToHashSet(), query.OrderByProperty, query.OrderByIsDescending, executionPlan);
                    }
                    else
                    {
                        return res;
                    }
                    
                    
                }


                // if only one AndQuery, process sequentially
                if (query.Elements.Count == 1)
                {
                    var andQuery = query.Elements[0];

                    var set = ProcessAndQuery(andQuery, executionPlan);

                    if (query.OrderByProperty != null)
                    {
                        return OrderBy(set.ToHashSet(), query.OrderByProperty, query.OrderByIsDescending, executionPlan);
                    }

                    return set.ToList();
                    
                }

                // if multiple AndQueries run in parallel
                HashSet<PackedObject> orResult = null;

                var results = new IList<PackedObject>[query.Elements.Count];

                Parallel.For(0, query.Elements.Count, i =>
                {
                    var andQuery = query.Elements[i];
                    results[i] = ProcessAndQuery(andQuery, executionPlan);
                });

                executionPlan.BeginMerge();

                // merge the results (they may contain duplicates)
                foreach (var result in results)
                {
                    if(orResult == null)
                        orResult = new HashSet<PackedObject>(result);
                    else
                        orResult.UnionWith(result);
                }

                executionPlan.EndMerge();

                if (query.OrderByProperty != null)
                {
                    return OrderBy(orResult, query.OrderByProperty, query.OrderByIsDescending, executionPlan);
                }

               
                return orResult!.ToList();
                
            }
            finally
            {
                executionPlan.End();
                ExecutionPlan = executionPlan;

                if (!query.CollectionName.Equals(LogEntry.Table, StringComparison.InvariantCultureIgnoreCase)) // do not log queries on @ACTIVITY table itself
                {
                    var type = query.CountOnly ? LogEntry.Eval : LogEntry.Select;
                    _log?.LogActivity(type, query.CollectionName, executionPlan.TotalTimeInMicroseconds, query.ToString(), query.Description(), executionPlan);

                }

                
            }
        }

        private IList<PackedObject> OrderBy(HashSet<PackedObject> selectedItems, string orderByProperty,
            in bool orderByIsDescending, ExecutionPlan executionPlan)
        {

            List<PackedObject> result = new List<PackedObject>(selectedItems.Count);

            var index = _dataStore.TryGetIndex(orderByProperty);

            if (index.IndexType == IndexType.Ordered)
            {
                executionPlan.BeginOrderBy();

                
                foreach (var o in index.GetAll(orderByIsDescending))
                {
                    if (selectedItems.Contains(o))
                    {
                        result.Add(o);
                    }
                }

                executionPlan.EndOrderBy();

                return result;
            }

            throw new CacheException("Order by can be used only on an ordered index");


        }

        IList<PackedObject> Distinct(IEnumerable<PackedObject> input, ExecutionPlan executionPlan, params int[] indexes)
        {
            executionPlan.BeginDistinct();

            List<PackedObject> result = new List<PackedObject>();
            HashSet<Projection> distinct = new HashSet<Projection>();
            foreach (var o in input)
            {
                var  projection = new Projection(o, indexes);
                if (distinct.Add(projection))
                {
                    result.Add(o);
                }
            }

            executionPlan.EndDistinct();

            return result;
        }


        /// <summary>
        /// Faster processing for simple query
        /// </summary>
        /// <param name="atomicQuery"></param>
        /// <param name="executionPlan">the global execution plan</param>
        /// <returns></returns>
        private IList<PackedObject> ProcessSimpleQuery(AtomicQuery atomicQuery, ExecutionPlan executionPlan)
        {
            var queryExecutionPlan = new QueryExecutionPlan(atomicQuery.ToString());
            executionPlan.QueryPlans.Add(queryExecutionPlan);

            queryExecutionPlan.SimpleQueryStrategy = true;

            if (atomicQuery.Operator == QueryOperator.Eq || atomicQuery.Operator == QueryOperator.In)
            {
                if (atomicQuery.IndexType == IndexType.Primary)
                {
                    queryExecutionPlan.UsedIndexes = new List<string>{_dataStore.PrimaryIndex.Name};
                    return _dataStore.PrimaryIndex.GetMany(atomicQuery.Values).ToList();
                }

            }


            var index = _dataStore.TryGetIndex(atomicQuery.PropertyName);

            if (index != null)
            {
                
                if (atomicQuery.Operator == QueryOperator.Eq) // works with all kinds of indexes
                {
                    try
                    {
                        queryExecutionPlan.Trace($"single index: {atomicQuery.PropertyName}");
                        queryExecutionPlan.StartIndexUse();
                        queryExecutionPlan.UsedIndexes = new List<string>{index.Name};
                        return index.GetMany(atomicQuery.Values).ToList();
                    }
                    finally
                    {
                        queryExecutionPlan.EndIndexUse();
                    }
                }

                if (index.IndexType == IndexType.Ordered && atomicQuery.IsComparison)
                {
                    try
                    {
                        queryExecutionPlan.Trace($"single index: {atomicQuery.PropertyName}");
                        queryExecutionPlan.StartIndexUse();
                        queryExecutionPlan.UsedIndexes = new List<string>{index.Name};
                        return index.GetMany(atomicQuery.Values, atomicQuery.Operator).ToList();
                    }
                    finally
                    {
                        queryExecutionPlan.EndIndexUse();
                    }
                }

                if (atomicQuery.Operator == QueryOperator.In || atomicQuery.Operator == QueryOperator.Contains)
                {
                    try
                    {
                        queryExecutionPlan.Trace($"single index: {atomicQuery.PropertyName}");
                        queryExecutionPlan.StartIndexUse();
                        queryExecutionPlan.UsedIndexes = new List<string>{index.Name};
                        return index.GetMany(atomicQuery.Values, atomicQuery.Operator).ToList();
                    }
                    finally
                    {
                        queryExecutionPlan.EndIndexUse();
                    }
                } 
            

            }

            // if we reached this point the only strategy left is full-scan
            queryExecutionPlan.FullScan = true;
            try
            {
                queryExecutionPlan.StartScan();
                return _dataStore.PrimaryIndex.GetAll().Where(atomicQuery.Match).ToList();
            }
            finally
            {
                queryExecutionPlan.EndScan();
            }

        }


        public ExecutionPlan ExecutionPlan { get; private set; }

        private void ProcessGetRequest(GetRequest request, IClient client)
        {

            try
            {

                var query = request.Query;

                var indexOfSelectedProperties = _dataStore.CollectionSchema.IndexesOfNames(query.SelectClause.Select(s => s.Name)
                    .ToArray());

                var result = ProcessQuery(query);

                var aliases = query.SelectClause.Select(s => s.Alias).ToArray();

                if (query.SelectClause.Count > 0)
                {
                    client.SendMany(result, indexOfSelectedProperties, aliases);
                }
                else
                {
                    client.SendMany(result, new int[0], null);
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

        public IList<PackedObject> 
            ProcessQuery(OrQuery query)
        {
            if (query.OnlyIfComplete)
            {
                var domainDescription = _dataStore.DomainDescription;
                var dataIsComplete = domainDescription != null && domainDescription.IsFullyLoaded;

                if (!dataIsComplete && domainDescription != null && !domainDescription.DescriptionAsQuery.IsEmpty())
                    dataIsComplete = query.IsSubsetOf(domainDescription.DescriptionAsQuery);


                if (!dataIsComplete)
                    throw new CacheException("Full data is not available for type " +
                                             _dataStore.CollectionSchema.CollectionName);
            }

            IList<PackedObject> result = new List<PackedObject>();


            // pure full-text search
            if (query.IsFullTextQuery && query.IsEmpty())
            {
                result = ProcessFullTextQuery(query.FullTextSearch, query.Take);
            }
            // mixed search : the result wil be the intersection of the full-text result and the query result
            else if (query.IsFullTextQuery)
            {
                ISet<PackedObject> queryResult = new HashSet<PackedObject>();
                IList<PackedObject> ftResult = new List<PackedObject>();
                Parallel.Invoke(
                    () => { queryResult = InternalProcessQuery(query).ToHashSet(); },
                    () => { ftResult = ProcessFullTextQuery(query.FullTextSearch, query.Take); });

                // the order will be the one in the full-text result (it is ranked by pertinence)
                foreach (var o in ftResult)
                {
                    if (queryResult.Contains(o))
                    {
                        result.Add(o);
                    }
                }
            }
            else // no full-text, simple query
            {
                result = InternalProcessQuery(query);
            }

            if (query.Distinct)
            {
                
                var indexOfSelectedProperties = _dataStore.CollectionSchema.IndexesOfNames(query.SelectClause.Select(s => s.Name)
                    .ToArray());

                result = Distinct(result, ExecutionPlan, indexOfSelectedProperties);
            }

            if (query.Take != 0)
            {
                result = result.Take(query.Take).ToList();
            }

            if (result.Count == 1)
            {
                _dataStore.Touch(result[0]);
            }


            _dataStore.IncrementReadCount();


            if (result.Count > 0)
            {
                _dataStore.IncrementHitCount();
            }

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

                            
                Parallel.ForEach(groupBy, new ParallelOptions { MaxDegreeOfParallelism = 10 },
                    objects =>
                    {
                        var pivot = new PivotLevel();

                        foreach (var o in objects)
                        {
                            pivot.AggregateOneObject(o, pivotRequest.AxisList, pivotRequest.ValuesList);    
                        }

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
                    Complete = _dataStore.DomainDescription != null && evalRequest.Query.IsSubsetOf(_dataStore.DomainDescription.DescriptionAsQuery)
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
}
