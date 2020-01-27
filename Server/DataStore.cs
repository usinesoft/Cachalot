#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Client;
using Client.ChannelInterface;
using Client.Core;
using Client.Interface;
using Client.Messages;
using Client.Profiling;
using Client.Queries;
using Client.Tools;
using Server.FullTextSearch;
using Server.Persistence;

#endregion

namespace Server
{
    /// <summary>
    ///     A data store contains multiply indexed objects of the same type
    ///     It implements a synchronization mechanism that executes read-only queries in parallel and queries that modify data
    ///     are serialized
    /// </summary>
    public class DataStore
    {

        public ISet<string> GetMostFrequentTokens(int max)
        {
            if (_fullTextIndex == null)
            {
                return new HashSet<string>();
            }

            return new HashSet<string>(_fullTextIndex.PositionsByToken.OrderByDescending(p => p.Value.Count).Select(p => p.Key).Take(max));
        }

        /// <summary>
        ///     List of indexes for index keys (multiple objects by key value)
        /// </summary>
        private readonly Dictionary<string, IndexBase> _dataByIndexKey;

        /// <summary>
        ///     Object by primary key
        /// </summary>
        private readonly Dictionary<KeyValue, CachedObject> _dataByPrimaryKey;

        /// <summary>
        ///     List of indexes for unique keys
        /// </summary>
        private readonly Dictionary<string, Dictionary<KeyValue, CachedObject>> _dataByUniqueKey;


        private readonly Dictionary<string, List<CachedObject>> _feedSessions =
            new Dictionary<string, List<CachedObject>>();

        private readonly FullTextIndex _fullTextIndex;

        /// <summary>
        ///     Will contain the name of the index used for the last query or null if a full scan was required
        /// </summary>
        private readonly ThreadLocal<ExecutionPlan> _lastExecutionPlan = new ThreadLocal<ExecutionPlan>();


        private long _count;

        /// <summary>
        ///     Description of data preloaded into the datastore
        /// </summary>
        private DomainDescription _domainDescription;

        private long _hitCount;


        private long _readCount;
        private long _updateCount;

        /// <summary>
        ///     Initialize an empty datastore from a type description
        /// </summary>
        /// <param name="typeDescription"></param>
        /// <param name="evictionPolicy"></param>
        /// <param name="config"></param>
        public DataStore(TypeDescription typeDescription, EvictionPolicy evictionPolicy, NodeConfig config)
        {
            TypeDescription = typeDescription ?? throw new ArgumentNullException(nameof(typeDescription));

            EvictionPolicy = evictionPolicy ?? throw new ArgumentNullException(nameof(evictionPolicy));

            //initialize the primary key dictionary
            _dataByPrimaryKey = new Dictionary<KeyValue, CachedObject>();


            //initialize the unique keys dictionaries (une by unique key) 
            _dataByUniqueKey = new Dictionary<string, Dictionary<KeyValue, CachedObject>>();

            foreach (var keyInfo in typeDescription.UniqueKeyFields)
                _dataByUniqueKey.Add(keyInfo.Name, new Dictionary<KeyValue, CachedObject>());

            //initialize the indexes (one by index key)
            _dataByIndexKey = new Dictionary<string, IndexBase>();

            // scalar indexed fields
            foreach (var indexField in typeDescription.IndexFields)
            {
                var index = IndexFactory.CreateIndex(indexField);
                _dataByIndexKey.Add(indexField.Name, index);
            }


            // list indexed fields
            foreach (var indexField in typeDescription.ListFields)
            {
                var index = IndexFactory.CreateIndex(indexField);
                _dataByIndexKey.Add(indexField.Name, index);
            }


            // create the full-text index if required
            if (typeDescription.FullText.Count > 0)
            {
                _fullTextIndex = new FullTextIndex(config.FullTextConfig)
                {
                    // a function that allows the full text engine to find the original line of text
                    LineProvider = pointer => _dataByPrimaryKey[pointer.PrimaryKey].FullText[pointer.Line]
                };
            }
        }

        public ReaderWriterLockSlim Lock { get; } = new ReaderWriterLockSlim();


        public ExecutionPlan LastExecutionPlan
        {
            get => _lastExecutionPlan.Value;
            private set => _lastExecutionPlan.Value = value;
        }

        public TypeDescription TypeDescription { get; }

        public EvictionType EvictionType => EvictionPolicy.Type;

        public long Count => Interlocked.Read(ref _count);

        public EvictionPolicy EvictionPolicy { get; set; }

        public Profiler Profiler { private get; set; }

        /// <summary>
        ///     Description of data preloaded into the datastore
        /// </summary>
        public DomainDescription DomainDescription
        {
            get => _domainDescription;
            private set => _domainDescription = value;
        }

        public long HitCount => Interlocked.Read(ref _hitCount);

        public long ReadCount => Interlocked.Read(ref _readCount);


        /// <summary>
        ///     object by primary key
        /// </summary>
        public Dictionary<KeyValue, CachedObject> DataByPrimaryKey => _dataByPrimaryKey;


        /// <summary>
        ///     Store a new object in all the indexes
        ///     REQUIRE: an object with the same primary key is not present in the datastore
        /// </summary>
        /// <param name="cachedObject"></param>
        /// <param name="excludeFromEviction">if true the item will never be evicted</param>
        internal void InternalAddNew(CachedObject cachedObject, bool excludeFromEviction)
        {
            InternalAddNew(cachedObject);

            Interlocked.Increment(ref _count);

            if (!excludeFromEviction)
                EvictionPolicy.AddItem(cachedObject);
        }


        public void Dump(DumpRequest request, int shardIndex)
        {
            InternalDump(request.Path, shardIndex);
        }

        private void InternalAddNew(CachedObject cachedObject)
        {
            if (cachedObject.FullTypeName != TypeDescription.FullTypeName)
                throw new InvalidOperationException(
                    $"An object of type {cachedObject.FullTypeName} can not be stored in DataStore of type {TypeDescription.FullTypeName}");


            var primaryKey = cachedObject.PrimaryKey;
            if (ReferenceEquals(primaryKey, null))
                throw new InvalidOperationException("can not store an object having a null primary key");


            _dataByPrimaryKey.Add(primaryKey, cachedObject);


            if (cachedObject.UniqueKeys != null)
                foreach (var uniqueKey in cachedObject.UniqueKeys)
                {
                    var dictionaryToUse = _dataByUniqueKey[uniqueKey.KeyName];
                    dictionaryToUse.Add(uniqueKey, cachedObject);
                }


            foreach (var index in _dataByIndexKey)
                index.Value.Put(cachedObject);


            if (cachedObject.FullText != null && cachedObject.FullText.Length > 0)
                _fullTextIndex.IndexDocument(cachedObject);
        }


        internal void LoadFromDump(string path, int shardIndex)
        {
            foreach (var cachedObject in DumpHelper.ObjectsInDump(path, TypeDescription, shardIndex))
            {
                InternalAddNew(cachedObject);

                // only in debug, only if this simulation was activated (for tests only)
                Dbg.SimulateException(100, shardIndex);
            }
        }

        private void InternalDump(string path, int shardIndex)
        {
            DumpHelper.DumpObjects(path, TypeDescription, shardIndex, _dataByPrimaryKey.Values);
        }

        /// <summary>
        ///     Remove the object from all indexes
        /// </summary>
        /// <param name="primary"></param>
        private CachedObject InternalRemoveByPrimaryKey(KeyValue primary)
        {
            Dbg.Trace($"remove by primary key {primary}");

            var toRemove = _dataByPrimaryKey[primary];
            _dataByPrimaryKey.Remove(primary);


            if (toRemove.UniqueKeys != null)
                foreach (var uniqueKey in toRemove.UniqueKeys)
                    _dataByUniqueKey[uniqueKey.KeyName].Remove(uniqueKey);


            foreach (var index in _dataByIndexKey)
                index.Value.RemoveOne(toRemove);

            _fullTextIndex?.DeleteDocument(primary);

            return toRemove;
        }


        internal void InternalTruncate()
        {
            EvictionPolicy.Clear();
            _dataByPrimaryKey.Clear();

            foreach (var indexByKey in _dataByUniqueKey)
                indexByKey.Value.Clear();

            foreach (var index in _dataByIndexKey)
                index.Value.Clear();

            _fullTextIndex?.Clear();

            Interlocked.Exchange(ref _updateCount, 0);
            Interlocked.Exchange(ref _readCount, 0);
            Interlocked.Exchange(ref _hitCount, 0);
            Interlocked.Exchange(ref _count, 0);

            // also reset the domain description
            Interlocked.Exchange(ref _domainDescription, null);
        }

        public void RemoveByPrimaryKey(KeyValue primary)
        {
            var removed = InternalRemoveByPrimaryKey(primary);
            if (removed != null)
            {
                EvictionPolicy.TryRemove(removed);

                Interlocked.Decrement(ref _count);
            }
        }

        /// <summary>
        ///     Update an object previously stored
        ///     The primary key must be the same, all others can change
        /// </summary>
        /// <param name="item"></param>
        private void InternalUpdate(CachedObject item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            if (!_dataByPrimaryKey.ContainsKey(item.PrimaryKey))
            {
                var msg = $"Update called for the object {item} which is not stored in the cache";
                throw new NotSupportedException(msg);
            }

            InternalRemoveByPrimaryKey(item.PrimaryKey);
            InternalAddNew(item);

            EvictionPolicy.Touch(item);
        }


        private void InternalRemoveMany(IList<CachedObject> items)
        {
            Dbg.Trace($"remove many called for {items.Count} items");

            foreach (var item in items)
            {
                if (!_dataByPrimaryKey.ContainsKey(item.PrimaryKey))
                    return;

                var toRemove = _dataByPrimaryKey[item.PrimaryKey];
                _dataByPrimaryKey.Remove(item.PrimaryKey);


                if (toRemove.UniqueKeys != null)
                    foreach (var uniqueKey in toRemove.UniqueKeys)
                        _dataByUniqueKey[uniqueKey.KeyName].Remove(uniqueKey);

                // if present remove it from the full-text index
                _fullTextIndex?.DeleteDocument(item.PrimaryKey);
            }


            foreach (var index in _dataByIndexKey)
                index.Value.RemoveMany(items);
        }


        /// <summary>
        ///     Get unique object by primary or unique key
        /// </summary>
        /// <param name="keyValue"></param>
        /// <returns></returns>
        internal CachedObject InternalGetOne(KeyValue keyValue)
        {
            CachedObject result = null;

            if (keyValue == (KeyValue) null)
                throw new ArgumentNullException(nameof(keyValue));

            if (keyValue.KeyType == KeyType.Primary)
                if (_dataByPrimaryKey.ContainsKey(keyValue))
                {
                    result = _dataByPrimaryKey[keyValue];
                    EvictionPolicy.Touch(result);
                }

            if (keyValue.KeyType == KeyType.Unique)
                if (_dataByUniqueKey.ContainsKey(keyValue.KeyName))
                    if (_dataByUniqueKey[keyValue.KeyName].ContainsKey(keyValue))
                    {
                        result = _dataByUniqueKey[keyValue.KeyName][keyValue];
                        EvictionPolicy.Touch(result);
                    }


            if (keyValue.KeyType == KeyType.Unique || keyValue.KeyType == KeyType.Primary) return result;


            throw new NotSupportedException(
                $"GetOne() called with the key {keyValue.KeyName} which is neither primary nor unique");
        }

        /// <summary>
        ///     Ges a subset by index key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        internal IList<CachedObject> InternalGetMany(KeyValue key)
        {
            if (key == (KeyValue) null)
                throw new ArgumentNullException(nameof(key));

            if (key.KeyType != KeyType.ScalarIndex)
                throw new ArgumentException("GetMany() called with a non index key", nameof(key));

            if (!_dataByIndexKey.ContainsKey(key.KeyName))
                throw new NotSupportedException($"GetMany() called with the unknown index key {key} ");

            return _dataByIndexKey[key.KeyName].GetMany(new List<KeyValue> {key}).ToList();
        }


        internal IList<CachedObject> InternalGetMany(KeyValue keyValue, QueryOperator op)
        {
            return InternalFind(new AtomicQuery(keyValue, op));
        }


        /// <summary>
        ///     Get a subset by index key value and comparison operator (atomic query)
        ///     Atomic queries re resolved by a single index
        /// </summary>
        /// <returns></returns>
        private IList<CachedObject> InternalFind(AtomicQuery atomicQuery, int count = 0)
        {
            if (!atomicQuery.IsValid)
                throw new NotSupportedException("Invalid atomic query: " + atomicQuery);

            var indexName = atomicQuery.IndexName;

            //////////////////////////////////////////////////////////////////////////
            // 3 cases: In + multiple values, Btw + 2 values, OTHER + 1 value


            if (atomicQuery.Operator == QueryOperator.In)
            {
                var result = new Dictionary<KeyValue, CachedObject>();

                foreach (var value in atomicQuery.InValues)
                {
                    if (count > 0 && result.Count >= count) break;

                    if (_dataByIndexKey.ContainsKey(value.KeyName))
                    {
                        var byOneValue = _dataByIndexKey[value.KeyName].GetMany(new List<KeyValue> {value});

                        foreach (var cachedObject in byOneValue)
                            result[cachedObject.PrimaryKey] = cachedObject; // override duplicates
                    }
                    else if (_dataByUniqueKey.ContainsKey(value.KeyName))
                    {
                        if (_dataByUniqueKey[value.KeyName].TryGetValue(value, out var cachedObject))
                            result[cachedObject.PrimaryKey] = cachedObject;
                    }
                    else if (TypeDescription.PrimaryKeyField.Name == value.KeyName)
                    {
                        if (_dataByPrimaryKey.TryGetValue(value, out var cachedObject))
                            result[cachedObject.PrimaryKey] = cachedObject;
                    }
                }


                LastExecutionPlan = new ExecutionPlan {PrimaryIndexName = indexName};

                return result.Values.ToList();
            }

            var index = _dataByIndexKey[indexName];
            var canBeProcessedByIndex = !(atomicQuery.Operator == QueryOperator.In && index.IsOrdered);

            if (atomicQuery.Operator != QueryOperator.Eq && atomicQuery.Operator != QueryOperator.In &&
                !index.IsOrdered)
                canBeProcessedByIndex = false;

            // if indexes can not be used proceed to full scan
            if (!canBeProcessedByIndex)
            {
                LastExecutionPlan = new ExecutionPlan(); // empty execution plan means full scan
                return _dataByPrimaryKey.Values.Where(atomicQuery.Match).ToList();
            }

            // Btw and comparison operators are managed by a single call to the index
            return _dataByIndexKey[indexName].GetMany(atomicQuery.Values, atomicQuery.Operator).ToList();
        }


        /// <summary>
        ///     Return a subset matching an OrQuery (sum of <see cref="AndQuery" /> (product of <see cref="AtomicQuery" />))
        ///     Will be decomposed as union of results of AND queries
        /// </summary>
        /// <param name="query"></param>
        /// <param name="onlyIfComplete"> </param>
        /// <returns></returns>
        internal IList<CachedObject> InternalGetMany(OrQuery query, bool onlyIfComplete = false)
        {
            var result = InternalFind(query, onlyIfComplete);

            EvictionPolicy.Touch(result);

            return result;
        }

        private IList<CachedObject> FullTextSearch(string query, int maxElements)
        {
            var result = _fullTextIndex.SearchBestDocuments(query, maxElements);

            return result.Select(r =>
            {
                // copy the score the CachedObject
                var item = _dataByPrimaryKey[r.PrimaryKey];
                item.Rank = r.Score;
                return item ;
            }).ToList();
        }

        private IList<CachedObject> InternalFind(OrQuery query, bool onlyIfComplete = false)
        {
            Dbg.Trace($"begin InternalFind with query {query}");


            if (onlyIfComplete)
            {
                var dataIsComplete = _domainDescription != null && _domainDescription.IsFullyLoaded;

                if (!dataIsComplete && _domainDescription != null && !_domainDescription.DescriptionAsQuery.IsEmpty())
                    dataIsComplete = query.IsSubsetOf(_domainDescription.DescriptionAsQuery);


                if (!dataIsComplete)
                    throw new CacheException("Full data is not available for type " + TypeDescription.FullTypeName);
            }


            // if empty query return all, unless there is a full-text query
            if (query.IsEmpty())
            {
                if (!query.IsFullTextQuery)
                {
                    Dbg.Trace($"InternalFind with empty query: return all {query.TypeName}");

                    return (query.Take > 0 ? _dataByPrimaryKey.Values.Take(query.Take) : _dataByPrimaryKey.Values)
                        .ToList();
                }

                // pure full-text search
                return FullTextSearch(query.FullTextSearch, query.Take);
            }

            var structuredResult = new HashSet<CachedObject>();

            // ignore full-text queries if no full-text index
            if (!query.IsFullTextQuery || _fullTextIndex == null)
            {
                InternalStructuredFind(query, structuredResult);

                return structuredResult.ToList();
            }

            // mixed query: full-text + structured
            // we return the intersection of the structured search and full-text search ordered by full-text score
            IList<CachedObject> ftResult = null;

            Parallel.Invoke(
                () => { InternalStructuredFind(query, structuredResult); },
                () => { ftResult = FullTextSearch(query.FullTextSearch, query.Take); });

            var result = new List<CachedObject>();

            foreach (var cachedObject in ftResult)
                if (structuredResult.Contains(cachedObject))
                    result.Add(cachedObject);

            Dbg.Trace($"end InternalFind returned {result.Count} ");

            return result;
        }

        private void InternalStructuredFind(OrQuery query, HashSet<CachedObject> result)
        {
            var take = query.Take;

            var remaining = take;
            foreach (var andQuery in query.Elements)
                // 0 means no limit
                if (take > 0)
                {
                    foreach (var item in InternalFind(andQuery, remaining)) result.Add(item);

                    if (result.Count < remaining)
                        remaining = take - result.Count;
                    else
                        break;
                }
                else
                {
                    foreach (var item in InternalFind(andQuery, remaining)) result.Add(item);
                }
        }

        /// <summary>
        ///     Remove a subset of items specified by a query
        /// </summary>
        /// <param name="query"></param>
        /// <returns>number of items effectively removed</returns>
        private int InternalRemoveMany(OrQuery query)
        {
            var toRemove = InternalFind(query);
            var result = toRemove.Count;

            InternalRemoveMany(toRemove);

            foreach (var cachedObject in toRemove)
            {
                Interlocked.Decrement(ref _count);

                EvictionPolicy.TryRemove(cachedObject);
            }

            return result;
        }


        /// <summary>
        ///     Count the items matching the specified query
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private int InternalEval(OrQuery query)
        {
            // for an empty query return the total number of elements
            if (query.Elements.Count == 0) return _dataByPrimaryKey.Count;

            var evalResult = query.Elements.Sum(InternalEval);

            // Take extension can be combined with Count extension
            return query.Take > 0 ? Math.Min(evalResult, query.Take) : evalResult;
        }

        /// <summary>
        ///     Count the items matched by an <see cref="AndQuery" />
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private int InternalEval(AndQuery query)
        {
            return InternalFind(query).Count;
        }


        /// <summary>
        ///     Returns a subset matching all the atomic queries of an <see cref="AndQuery" />
        /// </summary>
        /// <param name="query"></param>
        /// <param name="count">If > 0 return only the first count elements</param>
        /// <returns></returns>
        private IList<CachedObject> InternalFind(AndQuery query, int count = 0)
        {
            // void queries are illegal
            if (query.Elements.Count == 0)
                throw new NotSupportedException("Can not process an empty query");


            /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // Simplified processing for atomic queries(no need to choose which index to use first, because only one is involved)

            if (query.Elements.Count == 1)
            {
                var atomicQuery = query.Elements[0];

                // multiple result query
                if (atomicQuery.IndexType == KeyType.ScalarIndex || atomicQuery.IndexType == KeyType.ListIndex ||
                    atomicQuery.Operator == QueryOperator.In)
                {
                    if (count > 0) return InternalFind(atomicQuery).Take(count).ToList();
                    return InternalFind(atomicQuery);
                }

                // single result query
                var item = InternalGetOne(atomicQuery.Value);

                if (item != null)
                    return new List<CachedObject> {item};

                return new List<CachedObject>();
            }

            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // Simple query optimizer
            // 1) Count the elements matched by each atomic query (indexes are optimized for counting: much faster than retrieving the elements)
            // 2) Use the one (the corresponding index) matching the minimum number of items as main query. This will produce a first subset of items (using the index)
            // 3) Iterate over the subset and eliminate the elements that do not match the rest of the queries

            var minimumItemsMatched = int.MaxValue;
            IndexBase primaryIndex = null;
            AtomicQuery primaryQuery = null;

            var foundWithUniqueKeys = new List<CachedObject>();

            foreach (var atomicQuery in query.Elements)
            {
                if (atomicQuery.Value?.KeyType == KeyType.Primary)
                {
                    if (_dataByPrimaryKey.TryGetValue(atomicQuery.Value, out var val))
                    {
                        foundWithUniqueKeys.Add(val);
                        continue;
                    }

                    // if the search by primary key failed no need to continue
                    return new CachedObject[0];
                }

                if (atomicQuery.Value?.KeyType == KeyType.Unique)
                {
                    if (_dataByUniqueKey[atomicQuery.Value.KeyName].TryGetValue(atomicQuery.Value, out var val))
                    {
                        foundWithUniqueKeys.Add(val);
                        continue;
                    }

                    // if the search by unique key failed no need to continue
                    return new CachedObject[0];
                }

                if (!_dataByIndexKey.ContainsKey(atomicQuery.IndexName))
                    throw new NotSupportedException(atomicQuery.IndexName + " is not an index key");

                var curIndex = _dataByIndexKey[atomicQuery.IndexName];
                var curItemsCount = curIndex.GetCount(atomicQuery.Values, atomicQuery.Operator);

                if (curItemsCount < minimumItemsMatched)
                {
                    minimumItemsMatched = curItemsCount;
                    primaryIndex = curIndex;
                    primaryQuery = atomicQuery;
                }
            }

            // Get a primary set directly using the index

            IEnumerable<CachedObject> primarySet;

            // in an efficient index was identified use it, otherwise do a full scan
            if (primaryIndex != null)
            {
                primarySet = primaryIndex.GetMany(primaryQuery.Values, primaryQuery.Operator);
                LastExecutionPlan = new ExecutionPlan
                {
                    PrimaryIndexName = primaryIndex.Name,
                    ElementsInPrimarySet = minimumItemsMatched
                };
            }
            else
            {
                primarySet = _dataByPrimaryKey.Values;
                // empty execution plan means a full scan was required
                LastExecutionPlan = new ExecutionPlan();
            }

            primarySet = primarySet.Union(foundWithUniqueKeys);

            // Match all the items in the primary set against the query

            // if an index was used remove the test that was already matched by the index
            var simplifiedQuery = query.Clone();

            if (primaryQuery != null)
                simplifiedQuery.Elements.Remove(primaryQuery);

            return count > 0 ? primarySet.Where(simplifiedQuery.Match).Take(count).ToList() : primarySet.Where(simplifiedQuery.Match).ToList();
        }


        /// <summary>
        /// </summary>
        /// <param name="items"></param>
        /// <param name="excludeFromEviction">used only for non persistent case</param>
        /// <param name="persistTransaction">external action that is responsible to persist a durable transaction</param>
        internal void InternalPutMany(IList<CachedObject> items, bool excludeFromEviction,
            Action<Transaction> persistTransaction)
        {
            var isBulkOperation = items.Count > Constants.BulkThreshold;

            var toUpdate = new List<CachedObject>();

            try
            {
                Dbg.Trace($"begin InternalPutMany with {items.Count} object");

                persistTransaction?.Invoke(new PutTransaction {Items = items});


                InternalBeginBulkInsert(isBulkOperation);

                foreach (var item in items)

                    if (_dataByPrimaryKey.ContainsKey(item.PrimaryKey))
                        toUpdate.Add(item);
                    else
                        InternalAddNew(item, excludeFromEviction);
            }
            finally
            {
                InternalEndBulkInsert(isBulkOperation);

                // update items outside the bulk insert

                if (toUpdate.Count > Constants.BulkThreshold) // bulk optimization for updates
                {
                    InternalRemoveMany(toUpdate);

                    InternalPutMany(toUpdate, true, null); // the transaction is already persisted
                }
                else
                {
                    foreach (var cachedObject in toUpdate) InternalUpdate(cachedObject);
                }

                foreach (var cachedObject in toUpdate) EvictionPolicy.Touch(cachedObject);


                Dbg.Trace($"end InternalPutMany with {items.Count} object");

                
                
            }
        }


        private void ProcessEviction()
        {
            if (EvictionPolicy.IsEvictionRequired)
            {
                var itemsToEvict = EvictionPolicy.DoEviction();

                foreach (var item in itemsToEvict)
                {
                    InternalRemoveByPrimaryKey(item.PrimaryKey);
                    Interlocked.Decrement(ref _count);
                }

                var requestDescription = string.Empty;

                var processedItems = itemsToEvict.Count;
                var requestType = "EVICTION";

                ServerLog.AddEntry(new ServerLogEntry(0, requestType, requestDescription,
                    processedItems));
            }
        }


        internal void ProcessRequest(DataRequest dataRequest, IClient client, Action<Transaction> persistTransaction)
        {
            var requestDescription = "";
            var processedItems = 0;
            var requestType = "UNKNOWN";

            // if this request is part of a transaction the persistence is managed at a higher level
            var insideTransaction = persistTransaction == null;


            try
            {
                Profiler.Start("data request");
                Response toSend = null;


                if (dataRequest.AccessType == DataAccessType.Write)
                {
                    if (dataRequest is PutRequest put)
                    {
                        if (put.SessionId != null)
                        {
                            if (!_feedSessions.TryGetValue(put.SessionId, out var alreadyReceived))
                            {
                                alreadyReceived = new List<CachedObject>();
                                _feedSessions[put.SessionId] = alreadyReceived;
                            }

                            alreadyReceived.AddRange(put.Items);

                            if (put.EndOfSession)
                            {
                                // if all the packets have been received put everything at once

                                _feedSessions.Remove(put.SessionId);

                                InternalPutMany(alreadyReceived, put.ExcludeFromEviction, persistTransaction);
                            }
                        }

                        else if (put.OnlyIfNew) // conditional insert
                        {
                            if (put.Items.Count != 1)
                                throw new NotSupportedException("TryAdd can be called only with exactly one item");

                            var addedNewItems = InternalTryAdd(put.Items.First(), persistTransaction);

                            toSend = new ItemsCountResponse(addedNewItems);
                        }
                        else if (put.Predicate != null) // conditional update
                        {
                            if (put.Items.Count != 1)
                                throw new NotSupportedException("UpdateIf can be called only with exactly one item");

                            InternalUpdateIf(put.Items.First(), put.Predicate, persistTransaction);
                        }
                        else
                        {
                            InternalPutMany(put.Items, put.ExcludeFromEviction, persistTransaction);
                        }

                        if (put.Items.Count > 1)
                            requestDescription = $"put {put.Items.Count} items";
                        else if (put.Items.Count == 1)
                            requestDescription = put.Items[0].ToString();

                        processedItems = put.Items.Count;
                        requestType = "PUT";
                    }
                    else if (dataRequest is RemoveRequest removeRequest1)
                    {
                        var primaryKeyToRemove = removeRequest1.PrimaryKey;

                        var item = _dataByPrimaryKey[primaryKeyToRemove];

                        persistTransaction?.Invoke(new DeleteTransaction {ItemsToDelete = {item}});

                        RemoveByPrimaryKey(primaryKeyToRemove);

                        requestDescription = $"{removeRequest1.PrimaryKey.KeyName} = {removeRequest1.PrimaryKey}";
                        processedItems = 1;
                        requestType = "REMOVE";
                    }

                    else if (dataRequest is RemoveManyRequest removeRequest)
                    {
                        //a void query implies a TRUNCATE operation
                        //A TRUNCATE operation also resets the hit ratio

                        if (removeRequest.Query.Elements == null || removeRequest.Query.Elements.Count == 0)
                        {
                            persistTransaction?.Invoke(new DeleteTransaction
                            {
                                ItemsToDelete = _dataByPrimaryKey.Values.ToList()
                            });


                            var items = _dataByPrimaryKey.Count;

                            InternalTruncate();
                            requestDescription = string.Format(TypeDescription.TypeName.ToUpper());
                            processedItems = items;
                            requestType = "TRUNCATE";

                            toSend = new ItemsCountResponse(items);
                        }
                        else
                        {
                            var items = InternalFind(removeRequest.Query);
                            persistTransaction?.Invoke(new DeleteTransaction
                            {
                                ItemsToDelete = items
                            });


                            var removedItems = InternalRemoveMany(removeRequest.Query);

                            requestDescription = $"{removeRequest.Query}";
                            processedItems = removedItems;
                            requestType = "REMOVE";

                            toSend = new ItemsCountResponse(removedItems);
                        }
                    }


                    else if (dataRequest is DomainDeclarationRequest domainRequest)
                    {
                        if (EvictionPolicy.Type != EvictionType.None)
                            throw new NotSupportedException(
                                "Can not make a domain declaration for a type if eviction is active");

                        DomainDescription = domainRequest.Description;
                    }

                    else if (dataRequest is EvictionSetupRequest evictionSetup)
                    {
                        if (DomainDescription != null && !DomainDescription.IsEmpty)
                            throw new NotSupportedException(
                                "Can not activate eviction on a type with a domain declaration");

                        EvictionPolicy = evictionSetup.Type == EvictionType.LessRecentlyUsed
                            ? new LruEvictionPolicy(evictionSetup.Limit, evictionSetup.ItemsToEvict)
                            : (EvictionPolicy) new NullEvictionPolicy();
                    }


                    if (client != null)
                        if (!insideTransaction) // if inside a transaction the response is sent by the higher level
                        {
                            if (toSend == null)
                                toSend = new NullResponse();

                            client.SendResponse(toSend);
                        }
                }
                else
                {
                    if (dataRequest is GetRequest getRequest1)
                    {
                        var query = getRequest1.Query;
                        var result = InternalGetMany(query, query.OnlyIfComplete);


                        requestDescription = "\n       --> " + query;
                        processedItems = result.Count;
                        requestType = "GET";

                        //we don't know how many items were expected so consider it as an atomic operation
                        //for the read counter

                        Interlocked.Increment(ref _readCount);

                        if (result.Count >= 1)
                            Interlocked.Increment(ref _hitCount);

                        client.SendMany(result);
                    }
                    else
                    {
                        if (dataRequest is GetDescriptionRequest getRequest)
                        {
                            var query = getRequest.Query;
                            var result = InternalGetMany(query);


                            requestDescription = query.ToString();
                            processedItems = result.Count;
                            requestType = "SELECT";

                            client.SendManyGeneric(result);
                        }
                        else
                        {
                            if (dataRequest is EvalRequest request1)
                            {
                                var query = request1.Query;
                                var count = InternalEval(query);

                                var completeDataAvailable = false;

                                if (_domainDescription != null)
                                    completeDataAvailable = query.IsSubsetOf(_domainDescription.DescriptionAsQuery);

                                requestDescription = query.ToString();
                                processedItems = count;
                                requestType = "EVAL";

                                var response = new EvalResponse {Items = count, Complete = completeDataAvailable};
                                client.SendResponse(response);
                            }

                            else
                            {
                                if (dataRequest is GetAvailableRequest request)
                                {
                                    var missingObjects = new List<KeyValue>();
                                    var foundObjects = new List<CachedObject>();

                                    InternalGetAvailableObjects(request.PrimaryKeys, request.MoreCriteria,
                                        missingObjects,
                                        foundObjects);

                                    foreach (var _ in missingObjects)
                                        Interlocked.Increment(ref _readCount);
                                    foreach (var _ in foundObjects)
                                    {
                                        Interlocked.Increment(ref _readCount);
                                        Interlocked.Increment(ref _hitCount);
                                    }

                                    requestDescription = "GET AVAILABLE: " + request.PrimaryKeys.Count;
                                    processedItems = foundObjects.Count;
                                    requestType = "GET MANY";

                                    client.SendMany(missingObjects, foundObjects);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                client?.SendResponse(new ExceptionResponse(ex));
            }
            finally
            {
                var data = Profiler.End();

                var needsPlan = dataRequest is GetRequest || dataRequest is EvalRequest ||
                                dataRequest is GetDescriptionRequest;

                if (LastExecutionPlan != null && needsPlan)
                    requestDescription += "[ plan=" + LastExecutionPlan + "]";
                LastExecutionPlan = null;

                ServerLog.AddEntry(new ServerLogEntry(data.TotalTimeMiliseconds, requestType, requestDescription,
                    processedItems));

                ProcessEviction();
            }
        }

        private void InternalUpdateIf(CachedObject newValue, OrQuery test, Action<Transaction> persistTransaction)
        {
            try
            {
                Dbg.Trace(
                    $"begin InternalUpdateIf with primary key {newValue.PrimaryKey} for type{newValue.FullTypeName}");

                if (!_dataByPrimaryKey.ContainsKey(newValue.PrimaryKey))
                {
                    Dbg.Trace(
                        $"item {newValue.PrimaryKey} for type{newValue.FullTypeName} not found. Conditional update failed");
                    throw new CacheException("Item not found. Conditional update failed");
                }

                var prevValue = _dataByPrimaryKey[newValue.PrimaryKey];

                if (test.Match(prevValue))
                {
                    persistTransaction?.Invoke(new PutTransaction {Items = {newValue}});

                    InternalUpdate(newValue);
                }
                else
                {
                    throw new CacheException("Condition not satisfied.Item not updated");
                }
            }
            finally
            {
                Dbg.Trace("end InternalUpdateIf");
            }
        }

        private int InternalTryAdd(CachedObject item, Action<Transaction> persistTransaction)
        {
            try
            {
                Dbg.Trace($"begin InternalTryAdd with primary key {item.PrimaryKey} for type{item.FullTypeName}");

                if (_dataByPrimaryKey.ContainsKey(item.PrimaryKey))
                {
                    Dbg.Trace($"item {item.PrimaryKey} for type{item.FullTypeName} already present");
                    return 0;
                }

                persistTransaction?.Invoke(new PutTransaction {Items = {item}});

                InternalAddNew(item, true);
            }
            finally
            {
                Dbg.Trace("end InternalTryAdd");
            }

            return 1;
        }


        private void InternalEndBulkInsert(bool transactional)
        {
            if (!transactional)
                return;

            Parallel.ForEach(_dataByIndexKey.Where(p => p.Value is OrderedIndex), pair => { pair.Value.EndFill(); });
        }

        private void InternalBeginBulkInsert(bool bulkInsertMode)
        {
            if (!bulkInsertMode)
                return;

            foreach (var index in _dataByIndexKey)
                index.Value.BeginFill();
        }


        /// <summary>
        ///     Get all available objects and return the missing ones
        ///     Each element from <see cref="keyValues" /> plus <see cref="moreCriteria" /> should match at most one object
        ///     If <see cref="moreCriteria" /> is null, then the keyValues should be unique ones
        /// </summary>
        /// <param name="keyValues"></param>
        /// <param name="moreCriteria"></param>
        /// <param name="missingObjects"></param>
        /// <param name="foundObjects"></param>
        private void InternalGetAvailableObjects(IEnumerable<KeyValue> keyValues, Query moreCriteria,
            ICollection<KeyValue> missingObjects, IList<CachedObject> foundObjects)
        {
            if (moreCriteria == null) //optimize search for unique keys
                foreach (var keyValue in keyValues)
                {
                    if (keyValue.KeyType != KeyType.Primary && keyValue.KeyType != KeyType.Unique)
                        throw new NotSupportedException("Not an unique key : " + keyValue.KeyName);

                    var found = InternalGetOne(keyValue);
                    if (found != null)
                        foundObjects.Add(found);
                    else
                        missingObjects.Add(keyValue);
                }
            else
                foreach (var keyValue in keyValues)
                {
                    IList<CachedObject> objects;

                    if (keyValue.KeyType == KeyType.ScalarIndex)
                    {
                        objects = InternalGetMany(keyValue);
                    }
                    else
                    {
                        var obj = InternalGetOne(keyValue);
                        objects = new List<CachedObject>();
                        if (obj != null)
                            objects.Add(obj);
                    }

                    var matchingObject = objects.FirstOrDefault(moreCriteria.Match);

                    if (matchingObject != null)
                        foundObjects.Add(matchingObject);
                    else
                        missingObjects.Add(keyValue);
                }

            EvictionPolicy.Touch(foundObjects);
        }


        public void CheckCondition(KeyValue primaryKey, OrQuery condition)
        {
            if (_dataByPrimaryKey.TryGetValue(primaryKey, out var item))
            {
                if (!condition.Match(item))
                    throw new CacheException(
                        $"Condition not satisfied for item {primaryKey} of type {TypeDescription.FullTypeName}",
                        ExceptionType.ConditionNotSatisfied);
            }
            else
            {
                throw new CacheException($"Item {primaryKey} of type {TypeDescription.FullTypeName} not found");
            }
        }
    }
}