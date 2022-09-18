#region

using Client;
using Client.Core;
using Client.Interface;
using Client.Messages;
using Client.Tools;
using Server.FullTextSearch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Constants = Server.Persistence.Constants;

#endregion

namespace Server
{
    /// <summary>
    ///     A data store contains multiply indexed objects of the same type
    ///     It may also contain a full-text index
    /// </summary>
    public class DataStore
    {
        #region read-only indexes

        public IReadOnlyIndex PrimaryIndex => new UniqueIndex(CollectionSchema.PrimaryKeyField.Name, DataByPrimaryKey);



        /// <summary>
        /// Find index by unique ase insensitive name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public IReadOnlyIndex TryGetIndex(string name)
        {
            if (string.Equals(CollectionSchema.PrimaryKeyField.Name, name, StringComparison.CurrentCultureIgnoreCase))
                return new UniqueIndex(CollectionSchema.PrimaryKeyField.Name, DataByPrimaryKey);

            return _dataByIndexKey.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.CurrentCultureIgnoreCase)).Value;
        }

        #endregion

        private readonly FullTextConfig _fullTextConfig;

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


        private readonly FullTextIndex _fullTextIndex;


        private long _hitCount;

        private long _readCount;


        public void IncrementReadCount()
        {
            Interlocked.Increment(ref _readCount);
        }

        public void IncrementHitCount()
        {
            Interlocked.Increment(ref _hitCount);
        }

        /// <summary>
        ///     Initialize an empty datastore from a type description
        /// </summary>
        /// <param name="collectionSchema"></param>
        /// <param name="evictionPolicy"></param>
        /// <param name="fullTextConfig"></param>
        public DataStore(CollectionSchema collectionSchema, EvictionPolicy evictionPolicy,
            FullTextConfig fullTextConfig)
        {
            _fullTextConfig = fullTextConfig;

            CollectionSchema = collectionSchema ?? throw new ArgumentNullException(nameof(collectionSchema));

            EvictionPolicy = evictionPolicy ?? throw new ArgumentNullException(nameof(evictionPolicy));

            //initialize the primary key dictionary
            DataByPrimaryKey = new Dictionary<KeyValue, PackedObject>();


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
                _fullTextIndex = new FullTextIndex(fullTextConfig)
                {
                    // a function that allows the full text engine to find the original line of text
                    LineProvider = pointer => DataByPrimaryKey[pointer.PrimaryKey].TokenizedFullText[pointer.Line]
                };
        }


        public CollectionSchema CollectionSchema { get; }

        public EvictionType EvictionType => EvictionPolicy.Type;


        public EvictionPolicy EvictionPolicy { get; set; }


        /// <summary>
        ///     Description of data preloaded into the datastore
        /// </summary>
        public DomainDescription DomainDescription { get; set; }

        public long HitCount => Interlocked.Read(ref _hitCount);

        public long ReadCount => Interlocked.Read(ref _readCount);


        /// <summary>
        ///     object by primary key
        /// </summary>
        public Dictionary<KeyValue, PackedObject> DataByPrimaryKey { get; }


        /// <summary>
        ///     Store a new object in all the indexes
        ///     REQUIRE: an object with the same primary key is not present in the datastore
        /// </summary>
        /// <param name="packedObject"></param>
        /// <param name="excludeFromEviction">if true the item will never be evicted</param>
        internal void InternalAddNew(PackedObject packedObject, bool excludeFromEviction)
        {
            InternalAddNew(packedObject);


            if (!excludeFromEviction)
                EvictionPolicy.AddItem(packedObject);
        }



        private void InternalAddNew(PackedObject packedObject)
        {
            if (packedObject.PrimaryKey.IsNull)
                throw new NotSupportedException(
                    $"Can not insert an object with null primary key: collection {CollectionSchema.CollectionName}");

            if (packedObject.CollectionName != CollectionSchema.CollectionName)
                throw new InvalidOperationException(
                    $"An object of type {packedObject.CollectionName} can not be stored in DataStore of type {CollectionSchema.CollectionName}");


            var primaryKey = packedObject.PrimaryKey;
            if (ReferenceEquals(primaryKey, null))
                throw new InvalidOperationException("can not store an object having a null primary key");


            DataByPrimaryKey.Add(primaryKey, packedObject);



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

        public void Dump(string path, int shardIndex)
        {
            DumpHelper.DumpObjects(path, CollectionSchema, shardIndex, DataByPrimaryKey.Values);
        }

        /// <summary>
        ///     Remove the object from all indexes
        /// </summary>
        /// <param name="primary"></param>
        private PackedObject InternalRemoveByPrimaryKey(KeyValue primary)
        {
            Dbg.Trace($"remove by primary key {primary}");

            var toRemove = DataByPrimaryKey[primary];
            DataByPrimaryKey.Remove(primary);

            foreach (var metadata in CollectionSchema.ServerSide)
            {

                if (metadata.IndexType == IndexType.Ordered || metadata.IndexType == IndexType.Dictionary)
                    _dataByIndexKey[metadata.Name].RemoveOne(toRemove);
            }


            _fullTextIndex?.DeleteDocument(primary);

            return toRemove;
        }


        public void Truncate()
        {
            EvictionPolicy.Clear();
            DataByPrimaryKey.Clear();


            foreach (var index in _dataByIndexKey)
                index.Value.Clear();

            _fullTextIndex?.Clear();


            Interlocked.Exchange(ref _readCount, 0);
            Interlocked.Exchange(ref _hitCount, 0);


            // also reset the domain description
            DomainDescription = null;

        }

        public PackedObject RemoveByPrimaryKey(KeyValue primary)
        {
            var removed = InternalRemoveByPrimaryKey(primary);
            if (removed != null) EvictionPolicy.TryRemove(removed);

            return removed;
        }

        /// <summary>
        ///     Update an object previously stored
        ///     The primary key must be the same, all others can change
        /// </summary>
        /// <param name="item"></param>
        public void InternalUpdate(PackedObject item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            if (!DataByPrimaryKey.ContainsKey(item.PrimaryKey))
            {
                var msg = $"Update called for the object {item} which is not stored in the cache";
                throw new NotSupportedException(msg);
            }

            InternalRemoveByPrimaryKey(item.PrimaryKey);
            InternalAddNew(item);

            EvictionPolicy.Touch(item);
        }


        public void RemoveMany(IList<PackedObject> items)
        {
            Dbg.Trace($"remove many called for {items.Count} items");

            foreach (var item in items)
            {

                // if present remove it from the full-text index
                _fullTextIndex?.DeleteDocument(item.PrimaryKey);

                EvictionPolicy.TryRemove(item);
            }

            foreach (var index in _dataByIndexKey)
                index.Value.RemoveMany(items);


            foreach (var o in items)
            {
                DataByPrimaryKey.Remove(o.PrimaryKey);
            }

        }


        public IList<PackedObject> FullTextSearch(string query, int maxElements)
        {
            if (_fullTextIndex != null)
            {
                var result = _fullTextIndex.SearchBestDocuments(query, maxElements);

                return result.Select(r =>
                {
                    // copy the score the PackedObject
                    var item = DataByPrimaryKey[r.PrimaryKey];
                    item.Rank = r.Score;
                    return item;
                }).ToList();
            }

            return new List<PackedObject>();
        }


        /// <summary>
        /// </summary>
        /// <param name="items"></param>
        /// <param name="excludeFromEviction">used only for non persistent case</param>
        internal void InternalPutMany(IList<PackedObject> items, bool excludeFromEviction)
        {
            var isBulkOperation = items.Count > Constants.BulkThreshold;

            var toUpdate = new List<PackedObject>();

            try
            {
                Dbg.Trace($"begin InternalPutMany with {items.Count} object");


                InternalBeginBulkInsert(isBulkOperation);

                foreach (var item in items)
                    if (DataByPrimaryKey.ContainsKey(item.PrimaryKey))
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

                    var valuesBefore = toUpdate.Select(i => DataByPrimaryKey[i.PrimaryKey]).ToList();
                    // remove the values as they were before
                    RemoveMany(valuesBefore);


                    // insert the new values
                    InternalPutMany(toUpdate, true);
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
        ///     Like a bulk insert on an empty datastore. Used internally to reindex data when type description changed
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
                    RemoveMany(toUpdate);

                    InternalPutMany(toUpdate, true);
                }
                else
                {
                    foreach (var cachedObject in toUpdate) InternalUpdate(cachedObject);
                }

                //TODO check if necessary
                foreach (var cachedObject in toUpdate) EvictionPolicy.Touch(cachedObject);
            }
        }

        private IEnumerable<string> InternalEnumerateAsJson()
        {
            foreach (var cachedObject in DataByPrimaryKey) yield return cachedObject.Value.GetJson(CollectionSchema);
        }


        public void ProcessEviction()
        {
            if (EvictionPolicy.IsEvictionRequired)
            {
                var itemsToEvict = EvictionPolicy.DoEviction();

                foreach (var item in itemsToEvict) InternalRemoveByPrimaryKey(item.PrimaryKey);

                var requestDescription = string.Empty;

                var processedItems = itemsToEvict.Count;
                var requestType = "EVICTION";

                ServerLog.AddEntry(new ServerLogEntry(0, requestType, requestDescription,
                    processedItems));
            }
        }


        public static DataStore Reindex(DataStore old, CollectionSchema newDescription)
        {
            var result = new DataStore(newDescription, old.EvictionPolicy, old._fullTextConfig);


            result.InternalReindex(old.InternalEnumerateAsJson()
                .Select(json => PackedObject.PackJson(json, newDescription)));


            return result;
        }


        private void InternalEndBulkInsert(bool transactional)
        {
            if (!transactional)
                return;

            var ordered = _dataByIndexKey.Where(p => p.Value is OrderedIndex).ToList();

            if (ordered.Any())
            {
                Parallel.ForEach(ordered, pair => { pair.Value.EndFill(); });
            }
            
        }

        private void InternalBeginBulkInsert(bool bulkInsertMode)
        {
            if (!bulkInsertMode)
                return;

            foreach (var index in _dataByIndexKey)
                index.Value.BeginFill();
        }



        public void Touch(PackedObject packedObject)
        {
            EvictionPolicy?.Touch(packedObject);
        }
    }
}