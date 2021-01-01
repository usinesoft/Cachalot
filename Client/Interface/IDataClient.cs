#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.Core;
using Client.Messages;
using Client.Messages.Pivot;
using Client.Queries;

#endregion

namespace Client.Interface
{
    /// <summary>
    ///     Low level interface
    /// </summary>
    internal interface IDataClient : IDisposable
    {
        ClusterInformation GetClusterInformation();

        ServerLog GetLog(int lastLines);

        /// <summary>
        ///     Set or reset the read-only mode
        /// </summary>
        /// <param name="rw"></param>
        void SetReadonlyMode(bool rw = false);

        /// <summary>
        ///     Delete all data and schema information
        /// </summary>
        void DropDatabase();

        /// <summary>
        ///     Generate <paramref name="quantity" /> unique identifiers
        ///     They are guaranteed to be unique but they are not necessary in a contiguous range
        /// </summary>
        /// <param name="generatorName">name of the generator</param>
        /// <param name="quantity">number of unique ids to generate</param>
        int[] GenerateUniqueIds(string generatorName, int quantity = 1);


        /// <summary>
        ///     Add a new object or update an existent one. In cache mode (no persistence) the object may be excluded from
        ///     eviction even if an eviction mode is specified for the collection.
        ///     This method is transactional
        /// </summary>
        /// <param name="collectionName"></param>
        /// <param name="item"></param>
        /// <param name="excludeFromEviction">if true, the item will never be automatically evicted </param>
        void Put(string collectionName, PackedObject item, bool excludeFromEviction = false);

        /// <summary>
        ///     Add or replace a collection of objects.
        ///     The database is fed by packet of <see cref="CacheClient.DefaultPacketSize" /> objects.
        ///     This method is transactional only on a single node cluster
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collectionName"></param>
        /// <param name="items"></param>
        /// <param name="excludeFromEviction"></param>
        /// <param name="packetSize"></param>
        void FeedMany(string collectionName, IEnumerable<PackedObject> items, bool excludeFromEviction, int packetSize = 50000);


        /// <summary>
        ///     Remove all the items matching a query
        ///     This method is transactional only on a single node cluster
        /// </summary>
        /// <param name="query"></param>
        /// <returns>number of items effectively removed</returns>
        int RemoveMany(OrQuery query);

        /// <summary>
        ///     Compute a pivot table server-side
        /// </summary>
        /// <param name="filter">the items for which the pivot is computed</param>
        /// <param name="axis">ordered axis list for pivot calculation</param>
        /// <returns></returns>
        PivotLevel ComputePivot(OrQuery filter, params string[] axis);

        /// <summary>
        ///     Clears one collection and also resets the hit ratio in pure cache mode. The schema information is preserved
        ///     It is much faster than <see cref="DataClient.RemoveMany" /> with a query that matches all data
        /// </summary>
        /// <returns></returns>
        int Truncate(string collectionName);

        /// <summary>
        ///     Retrieve multiple objects a json using a precompiled query.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="sessionId">optional session id for consistent reads</param>
        /// <returns></returns>
        IEnumerable<RankedItem> GetMany(OrQuery query, Guid sessionId = default(Guid));

        
        /// <summary>
        ///     Check if the cluster is up and running
        /// </summary>
        /// <returns></returns>
        bool Ping();


        /// <summary>
        ///     If it is a new collection create it on the server. The schema contains all the indexing parameters
        ///     If schema is different that the one registered on the server this will trigger reindexing which may take significant time
        /// </summary>
        /// <param name="collectionName"></param>
        /// <param name="schema"></param>
        /// <param name="shard">to specify only if the cluster configuration changed</param>
        void DeclareCollection(string collectionName, CollectionSchema schema, int shard = -1);


        /// <summary>
        ///     Count the items matching the query and check for data completeness.
        /// </summary>
        /// <param name="query"></param>
        /// <returns>completeness, items count</returns>
        Tuple<bool, int> EvalQuery(OrQuery query);

        /// <summary>
        ///     Dump all data to an external directory
        /// </summary>
        /// <param name="path"></param>
        void Dump(string path);

        /// <summary>
        ///     Reinitialize all data from a dump. The number of node in the cluster may not change between Dump and ImportDump
        /// </summary>
        /// <param name="path"></param>
        void ImportDump(string path);

        /// <summary>
        ///     This is slower than ImportDump but it allows to change the number of nodes in the database
        /// </summary>
        void InitializeFromDump(string path);


        /// <summary>
        ///     Stop or restart all the servers in the cluster
        /// </summary>
        /// <param name="restart"></param>
        void Stop(bool restart);


        /// <summary>
        ///     Put and delete items in an ACID transaction
        /// </summary>
        /// <param name="itemsToPut">objects to insert or update in the transaction</param>
        /// <param name="conditions">
        ///     A list of conditions for conditional update. Needs to have the same size as itemsToPut. May
        ///     contain null values
        /// </param>
        /// <param name="itemsToDelete">items to delete inside a transaction</param>
        void ExecuteTransaction(IList<PackedObject> itemsToPut, IList<OrQuery> conditions,
            IList<PackedObject> itemsToDelete = null);


        /// <summary>
        /// Import data from a json file into a collection
        /// </summary>
        /// <param name="collectionName"></param>
        /// <param name="jsonFile"></param>
        void Import(string collectionName, string jsonFile);

        /// <summary>
        /// Add an object only if it was not already there and return true if it was effectively added
        /// Thi method is transactional
        /// </summary>
        /// <param name="collectionName"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        bool TryAdd(string collectionName, PackedObject item);

        /// <summary>
        /// Update an object only if the condition (applied on the previous version in the server) is satisfied
        /// Thi method is transactional
        /// </summary>
        /// <param name="newValue"></param>
        /// <param name="testAsQuery"></param>
        void UpdateIf(PackedObject newValue, OrQuery testAsQuery);

        /// <summary>
        /// Acquire a server side lock and return a session id if successful (default(guid) otherwise). The session id can be used for multiple calls to <see cref="GetMany"/>
        /// during a session of consistent reads
        /// </summary>
        /// <param name="writeAccess"></param>
        /// <param name="collections"></param>
        /// <returns></returns>
        Guid AcquireLock(bool writeAccess, params string[] collections);

        void ReleaseLock(Guid sessionId);

        #region cache only methods

        /// <summary>
        ///     Mark a data type as fully loaded. GetMany requests may require that data is fully loaded
        /// </summary>
        /// <param name="collectionName"></param>
        /// <param name="isFullyLoaded"></param>
        void DeclareDataFullyLoaded(string collectionName, bool isFullyLoaded);


        /// <summary>
        ///     Check that data is fully loaded for the specified type
        /// </summary>
        /// <returns></returns>
        bool IsDataFullyLoaded(string collectionName);

        /// <summary>
        ///     Declare a subset of data as being fully available in the cache.<br />
        ///     Used by loader components to declare data preloaded in the cache.
        ///     <seealso cref="DomainDescription" />
        /// </summary>
        /// <param name="domain">data description</param>
        void DeclareDomain(DomainDescription domain);


        /// <summary>
        ///     Activate eviction for a type name. If LRU eviction is used when the limit is reached
        ///     the less recently used <paramref name="itemsToRemove" /> items will be evicted
        ///     If TTL (time to live) eviction is used then objects are removed if they are older than a specified time-span
        /// </summary>
        /// <param name="collectionName"></param>
        /// <param name="evictionType"></param>
        /// <param name="limit"></param>
        /// <param name="itemsToRemove"></param>
        /// <param name="timeLimitInMilliseconds"></param>
        void ConfigEviction(string collectionName, EvictionType evictionType, int limit, int itemsToRemove,
            int timeLimitInMilliseconds);

        #endregion
    }
}