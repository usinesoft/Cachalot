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
using Client.Messages.Pivot;
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
        private readonly NodeConfig _config;

        public ISet<string> GetMostFrequentTokens(int max)
        {
            if (_fullTextIndex == null) return new HashSet<string>();

            return new HashSet<string>(_fullTextIndex.PositionsByToken.OrderByDescending(p => p.Value.Count)
                .Select(p => p.Key).Take(max));
        }

        /// <summary>
        ///     List of indexes for index keys (multiple objects by key value)
        /// </summary>
        private readonly Dictionary<string, IndexBase> _dataByIndexKey;

        /// <summary>
        ///     Object by primary key
        /// </summary>
        private readonly Dictionary<KeyValue, PackedObject> _dataByPrimaryKey;

        /// <summary>
        ///     List of indexes for unique keys
        /// </summary>
        private readonly Dictionary<string, Dictionary<KeyValue, PackedObject>> _dataByUniqueKey;


        private readonly Dictionary<Guid, List<PackedObject>> _feedSessions =
            new Dictionary<Guid, List<PackedObject>>();

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
        /// <param name="collectionSchema"></param>
        /// <param name="evictionPolicy"></param>
        /// <param name="config"></param>
        public DataStore(CollectionSchema collectionSchema, EvictionPolicy evictionPolicy, NodeConfig config)
        {
            _config = config;

            CollectionSchema = collectionSchema ?? throw new ArgumentNullException(nameof(collectionSchema));

            EvictionPolicy = evictionPolicy ?? throw new ArgumentNullException(nameof(evictionPolicy));

            //initialize the primary key dictionary
            _dataByPrimaryKey = new Dictionary<KeyValue, PackedObject>();


            //initialize the unique keys dictionaries (one by unique key) 
            _dataByUniqueKey = new Dictionary<string, Dictionary<KeyValue, PackedObject>>();

            foreach (var keyInfo in collectionSchema.UniqueKeyFields)
                _dataByUniqueKey.Add(keyInfo.Name, new Dictionary<KeyValue, PackedObject>());

            //initialize the indexes (one by index key)
            _dataByIndexKey = new Dictionary<string, IndexBase>();

            // scalar indexed fields
            foreach (var indexField in collectionSchema.IndexFields)
            {
                var index = IndexFactory.CreateIndex(indexField);
                _dataByIndexKey.Add(indexField.Name, index);
            }



            // create the full-text index if required
            if (collectionSchema.FullText.Count > 0)
                _fullTextIndex = new FullTextIndex(config.FullTextConfig)
                {
                    // a function that allows the full text engine to find the original line of text
                    LineProvider = pointer => _dataByPrimaryKey[pointer.PrimaryKey].TokenizedFullText[pointer.Line]
                };
        }

        public ExecutionPlan LastExecutionPlan
        {
            get => _lastExecutionPlan.Value;
            private set => _lastExecutionPlan.Value = value;
        }

        public CollectionSchema CollectionSchema { get; }

        public EvictionType EvictionType => EvictionPolicy.Type;

        public long Count => Interlocked.Read(ref _count);

        public EvictionPolicy EvictionPolicy { get; private set; }

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
        public Dictionary<KeyValue, PackedObject> DataByPrimaryKey => _dataByPrimaryKey;


        /// <summary>
        ///     Store a new object in all the indexes
        ///     REQUIRE: an object with the same primary key is not present in the datastore
        /// </summary>
        /// <param name="packedObject"></param>
        /// <param name="excludeFromEviction">if true the item will never be evicted</param>
        internal void InternalAddNew(PackedObject packedObject, bool excludeFromEviction)
        {
            InternalAddNew(packedObject);

            Interlocked.Increment(ref _count);

            if (!excludeFromEviction)
                EvictionPolicy.AddItem(packedObject);
        }


        public void Dump(DumpRequest request, int shardIndex)
        {
            InternalDump(request.Path, shardIndex);
        }

        private void InternalAddNew(PackedObject packedObject)
        {
            if(packedObject.PrimaryKey.IsNull)
                throw new NotSupportedException($"Can not insert an object with null primary key: collection {CollectionSchema.CollectionName}");

            if (packedObject.CollectionName != CollectionSchema.CollectionName)
                throw new InvalidOperationException(
                    $"An object of type {packedObject.CollectionName} can not be stored in DataStore of type {CollectionSchema.CollectionName}");


            var primaryKey = packedObject.PrimaryKey;
            if (ReferenceEquals(primaryKey, null))
                throw new InvalidOperationException("can not store an object having a null primary key");


            _dataByPrimaryKey.Add(primaryKey, packedObject);


            if (packedObject.UniqueKeys != null)
                foreach (var uniqueKey in packedObject.UniqueKeys)
                {
                    var dictionaryToUse = _dataByUniqueKey[uniqueKey.KeyName];
                    dictionaryToUse.Add(uniqueKey, packedObject);
                }


            foreach (var index in _dataByIndexKey)
                index.Value.Put(packedObject);


            if (packedObject.FullText != null && packedObject.FullText.Length > 0)
                _fullTextIndex.IndexDocument(packedObject);
        }


        internal void LoadFromDump(string path, int shardIndex)
        {
            foreach (var cachedObject in DumpHelper.ObjectsInDump(path, CollectionSchema, shardIndex))
            {
                InternalAddNew(cachedObject);

                // only in debug, only if this simulation was activated (for tests only)
                Dbg.SimulateException(100, shardIndex);
            }
        }

        private void InternalDump(string path, int shardIndex)
        {
            DumpHelper.DumpObjects(path, CollectionSchema, shardIndex, _dataByPrimaryKey.Values);
        }

        /// <summary>
        ///     Remove the object from all indexes
        /// </summary>
        /// <param name="primary"></param>
        private PackedObject InternalRemoveByPrimaryKey(KeyValue primary)
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
        private void InternalUpdate(PackedObject item)
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


        private void InternalRemoveMany(IList<PackedObject> items)
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
        internal PackedObject InternalGetOne(KeyValue keyValue)
        {
            PackedObject result;

            if(keyValue.KeyType != IndexType.Primary && keyValue.KeyType != IndexType.Unique) throw new NotSupportedException(
                $"GetOne() called with the key {keyValue.KeyName} which is neither primary nor unique");

            if (keyValue == null)
                throw new ArgumentNullException(nameof(keyValue));

            if (keyValue.KeyType ==  IndexType.Primary)
                if (_dataByPrimaryKey.ContainsKey(keyValue))
                {
                    result = _dataByPrimaryKey[keyValue];
                    EvictionPolicy.Touch(result);

                    return result;
                }

            if (keyValue.KeyType == IndexType.Unique)
                if (_dataByUniqueKey.ContainsKey(keyValue.KeyName))
                    if (_dataByUniqueKey[keyValue.KeyName].ContainsKey(keyValue))
                    {
                        result = _dataByUniqueKey[keyValue.KeyName][keyValue];
                        EvictionPolicy.Touch(result);

                        return result;
                    }

            // return null if not found
            return null;

        }

        /// <summary>
        ///     Ges a subset by index key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        internal IList<PackedObject> InternalGetMany(KeyValue key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (key.KeyType != IndexType.Ordered && key.KeyType != IndexType.Dictionary)
                throw new ArgumentException("GetMany() called with a non index key", nameof(key));

            if (!_dataByIndexKey.ContainsKey(key.KeyName))
                throw new NotSupportedException($"GetMany() called with the unknown index key {key} ");

            return _dataByIndexKey[key.KeyName].GetMany(new List<KeyValue> {key}).ToList();
        }


        internal IList<PackedObject> InternalGetMany(KeyValue keyValue, QueryOperator op)
        {
            return InternalFind(new AtomicQuery(keyValue, op));
        }


        /// <summary>
        ///     Get a subset by index key value and comparison operator (atomic query)
        ///     Atomic queries re resolved by a single index
        /// </summary>
        /// <returns></returns>
        private IList<PackedObject> InternalFind(AtomicQuery atomicQuery, int count = 0)
        {
            if (!atomicQuery.IsValid)
                throw new NotSupportedException("Invalid atomic query: " + atomicQuery);

            var indexName = atomicQuery.IndexName;

            //////////////////////////////////////////////////////////////////////////
            // 3 cases: In + multiple values, Btw + 2 values, OTHER + 1 value


            if (atomicQuery.Operator == QueryOperator.In)
            {
                var result = new Dictionary<KeyValue, PackedObject>();

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
                    else if (CollectionSchema.PrimaryKeyField.Name == value.KeyName)
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
        internal IList<PackedObject> InternalGetMany(OrQuery query, bool onlyIfComplete = false)
        {
            var result = InternalFind(query, onlyIfComplete);

            // important decision after lots of thinking. A get many on a datastore does not make much sense so we will considered 
            // the item "touched" only if its a single one. Otherwise the eviction order may be shuffled as the primary key index is not ordered
            if (result.Count == 1) EvictionPolicy.Touch(result);


            return result;
        }

        private IList<PackedObject> FullTextSearch(string query, int maxElements)
        {
            var result = _fullTextIndex.SearchBestDocuments(query, maxElements);

            return result.Select(r =>
            {
                // copy the score the PackedObject
                var item = _dataByPrimaryKey[r.PrimaryKey];
                item.Rank = r.Score;
                return item;
            }).ToList();
        }

        private IList<PackedObject> InternalFind(OrQuery query, bool onlyIfComplete = false)
        {
            Dbg.Trace($"begin InternalFind with query {query}");


            if (onlyIfComplete)
            {
                var dataIsComplete = _domainDescription != null && _domainDescription.IsFullyLoaded;

                if (!dataIsComplete && _domainDescription != null && !_domainDescription.DescriptionAsQuery.IsEmpty())
                    dataIsComplete = query.IsSubsetOf(_domainDescription.DescriptionAsQuery);


                if (!dataIsComplete)
                    throw new CacheException("Full data is not available for type " + CollectionSchema.CollectionName);
            }


            // if empty query return all, unless there is a full-text query
            if (query.IsEmpty())
            {
                if (!query.IsFullTextQuery)
                {
                    Dbg.Trace($"InternalFind with empty query: return all {query.CollectionName}");

                    
                    return (query.Take > 0 ? _dataByPrimaryKey.Values.Take(query.Take) : _dataByPrimaryKey.Values)
                        .ToList();
                }

                // pure full-text search
                return FullTextSearch(query.FullTextSearch, query.Take);
            }

            var structuredResult = new HashSet<PackedObject>();

            // ignore full-text queries if no full-text index
            if (!query.IsFullTextQuery || _fullTextIndex == null)
            {
                InternalStructuredFind(query, structuredResult);

                return structuredResult.ToList();
            }

            // mixed query: full-text + structured
            // we return the intersection of the structured search and full-text search ordered by full-text score
            IList<PackedObject> ftResult = null;

            Parallel.Invoke(
                () => { InternalStructuredFind(query, structuredResult); },
                () => { ftResult = FullTextSearch(query.FullTextSearch, query.Take); });

            var result = new List<PackedObject>();

            foreach (var cachedObject in ftResult)
                if (structuredResult.Contains(cachedObject))
                    result.Add(cachedObject);

            Dbg.Trace($"end InternalFind returned {result.Count} ");

            return result;
        }

        private void InternalStructuredFind(OrQuery query, HashSet<PackedObject> result)
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
        private IList<PackedObject> InternalFind(AndQuery query, int count = 0)
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
                if (atomicQuery.IndexType == IndexType.Ordered || atomicQuery.IndexType == IndexType.Dictionary ||
                    atomicQuery.Operator == QueryOperator.In)
                {
                    if (count > 0) return InternalFind(atomicQuery).Take(count).ToList();
                    return InternalFind(atomicQuery);
                }

                // single result query
                var item = InternalGetOne(atomicQuery.Value);

                if (item != null)
                    return new List<PackedObject> {item};

                return new List<PackedObject>();
            }

            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // Simple query optimizer
            // 1) Count the elements matched by each atomic query (indexes are optimized for counting: much faster than retrieving the elements)
            // 2) Use the one (the corresponding index) matching the minimum number of items as main query. This will produce a first subset of items (using the index)
            // 3) Iterate over the subset and eliminate the elements that do not match the rest of the queries

            var minimumItemsMatched = int.MaxValue;
            IndexBase primaryIndex = null;
            AtomicQuery primaryQuery = null;

            var foundWithUniqueKeys = new List<PackedObject>();

            foreach (var atomicQuery in query.Elements)
            {
                if (atomicQuery.Value?.KeyType == IndexType.Primary)
                {
                    // TODO allow other operators
                    if (atomicQuery.Operator != QueryOperator.Eq)
                    {
                        throw new NotSupportedException("On primary key only EQUALS operator is supported");
                    }
                    if (_dataByPrimaryKey.TryGetValue(atomicQuery.Value, out var val))
                    {
                        foundWithUniqueKeys.Add(val);
                        continue;
                    }

                    // if the search by primary key failed no need to continue
                    return new PackedObject[0];
                }

                if (atomicQuery.Value?.KeyType == IndexType.Unique)
                {
                    // TODO allow other operators
                    if (atomicQuery.Operator != QueryOperator.Eq)
                    {
                        throw new NotSupportedException("On unique key only EQUALS operator is supported");
                    }

                    if (_dataByUniqueKey[atomicQuery.Value.KeyName].TryGetValue(atomicQuery.Value, out var val))
                    {
                        foundWithUniqueKeys.Add(val);
                        continue;
                    }

                    // if the search by unique key failed no need to continue
                    return new PackedObject[0];
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

            // do not work too hard if the most specific index returned 0
            if (minimumItemsMatched == 0)
                return new List<PackedObject>();

            // Get a primary set directly using the index

            IEnumerable<PackedObject> primarySet;

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

            return count > 0
                ? primarySet.Where(simplifiedQuery.Match).Take(count).ToList()
                : primarySet.Where(simplifiedQuery.Match).ToList();
        }


        /// <summary>
        /// </summary>
        /// <param name="items"></param>
        /// <param name="excludeFromEviction">used only for non persistent case</param>
        /// <param name="persistTransaction">external action that is responsible to persist a durable transaction</param>
        internal void InternalPutMany(IList<PackedObject> items, bool excludeFromEviction,
            Action<Transaction> persistTransaction)
        {
            var isBulkOperation = items.Count > Constants.BulkThreshold;

            var toUpdate = new List<PackedObject>();

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


        /// <summary>
        /// Like a bulk insert on an empty datastore. Used internally to reindex data when type description changed
        /// </summary>
        /// <param name="items"></param>
        private void InternalReindex(IEnumerable<PackedObject> items)
        {
            
            var toUpdate = new List<PackedObject>();

            try
            {
             
                InternalBeginBulkInsert(true);

                foreach (var item in items)
                    InternalAddNew(item, true);
            }
            finally
            {
                InternalEndBulkInsert(true);

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

            }
        }

        IEnumerable<string> InternalEnumerateAsJson()
        {
            foreach (var cachedObject in DataByPrimaryKey)
            {
                yield return cachedObject.Value.Json;
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
                        if (put.SessionId != default)
                        {
                            if (!_feedSessions.TryGetValue(put.SessionId, out var alreadyReceived))
                            {
                                alreadyReceived = new List<PackedObject>();
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
                            requestDescription = string.Format(CollectionSchema.TypeName.ToUpper());
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
                            : evictionSetup.Type == EvictionType.TimeToLive
                                ? new TtlEvictionPolicy(
                                    TimeSpan.FromMilliseconds(evictionSetup.TimeToLiveInMilliseconds))
                                : (EvictionPolicy) new NullEvictionPolicy();
                    }


                    if (client != null)
                        if (!insideTransaction) // if inside a transaction the response is sent by the higher level
                        {
                            toSend ??= new NullResponse();

                            client.SendResponse(toSend);
                        }
                }
                else // read-only requests
                {
                    switch (dataRequest)
                    {
                        case GetRequest getRequest:

                            var result = InternalGetMany(getRequest.Query, getRequest.Query.OnlyIfComplete);

                            requestDescription = "\n       --> " + getRequest.Query;
                            processedItems = result.Count;
                            requestType = "GET";

                            //we don't know how many items were expected so consider it as an atomic operation
                            //for the read counter

                            Interlocked.Increment(ref _readCount);

                            if (result.Count >= 1)
                                Interlocked.Increment(ref _hitCount);

                            client.SendMany(result);
                            break;

                        
                        case EvalRequest evalRequest:

                            var count = InternalEval(evalRequest.Query);

                            var completeDataAvailable = false;

                            if (_domainDescription != null)
                                completeDataAvailable =
                                    evalRequest.Query.IsSubsetOf(_domainDescription.DescriptionAsQuery);

                            requestDescription = evalRequest.Query.ToString();
                            processedItems = count;
                            requestType = "EVAL";

                            var response = new EvalResponse {Items = count, Complete = completeDataAvailable};
                            client.SendResponse(response);
                            break;

                        case PivotRequest pivotRequest:

                            var filtered = InternalFind(pivotRequest.Query);

                            requestDescription = "PIVOT on: " +  pivotRequest.Query;

                            requestType = "PIVOT";

                            var pr = new PivotResponse();

                            // partition data to allow for parallel calculation
                            var groupBy = filtered.GroupBy(i => i.PrimaryKey.GetHashCode() % 10);

                            
                            Parallel.ForEach(groupBy, new ParallelOptions { MaxDegreeOfParallelism = 10 },
                                objects =>
                            {
                                var pivot = new PivotLevel();

                                foreach (var o in objects)
                                {
                                    pivot.AggregateOneObject(o, pivotRequest.AxisList.ToArray());    
                                }

                                lock (pr)
                                {
                                    pr.Root.MergeWith(pivot);
                                }
                                
                            });

                            //foreach (var packedObject in filtered)
                            //{
                            //    pr.Root.AggregateOneObject(packedObject, pivotRequest.AxisList.ToArray());
                            //}

                            client.SendResponse(pr);
                            break;

                        
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

                var needsPlan = dataRequest is GetRequest || dataRequest is EvalRequest;
                                

                if (LastExecutionPlan != null && needsPlan)
                    requestDescription += "[ plan=" + LastExecutionPlan + "]";
                LastExecutionPlan = null;

                ServerLog.AddEntry(new ServerLogEntry(data.TotalTimeMiliseconds, requestType, requestDescription,
                    processedItems));

                ProcessEviction();
            }
        }


        public static DataStore Reindex(DataStore old, CollectionSchema newDescription)
        {
            var result = new DataStore(newDescription, old.EvictionPolicy, old._config) {Profiler = old.Profiler};


            result.InternalReindex(old.InternalEnumerateAsJson().Select(json=> PackedObject.PackJson(json, newDescription)));


            return result;
        }

        private void InternalUpdateIf(PackedObject newValue, OrQuery test, Action<Transaction> persistTransaction)
        {
            try
            {
                Dbg.Trace(
                    $"begin InternalUpdateIf with primary key {newValue.PrimaryKey} for type{newValue.CollectionName}");

                if (!_dataByPrimaryKey.ContainsKey(newValue.PrimaryKey))
                {
                    Dbg.Trace(
                        $"item {newValue.PrimaryKey} for type{newValue.CollectionName} not found. Conditional update failed");
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

        private int InternalTryAdd(PackedObject item, Action<Transaction> persistTransaction)
        {
            try
            {
                Dbg.Trace($"begin InternalTryAdd with primary key {item.PrimaryKey} for type{item.CollectionName}");

                if (_dataByPrimaryKey.ContainsKey(item.PrimaryKey))
                {
                    Dbg.Trace($"item {item.PrimaryKey} for type{item.CollectionName} already present");
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


        

        public void CheckCondition(KeyValue primaryKey, OrQuery condition)
        {
            if (_dataByPrimaryKey.TryGetValue(primaryKey, out var item))
            {
                if (!condition.Match(item))
                    throw new CacheException(
                        $"Condition not satisfied for item {primaryKey} of type {CollectionSchema.CollectionName}",
                        ExceptionType.ConditionNotSatisfied);
            }
            else
            {
                throw new CacheException($"Item {primaryKey} of type {CollectionSchema.CollectionName} not found");
            }
        }
    }
}