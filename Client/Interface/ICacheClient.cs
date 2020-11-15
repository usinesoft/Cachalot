#region

using System;
using System.Collections.Generic;
using Client.Core;
using Client.Messages;
using Client.Messages.Pivot;
using Client.Queries;

#endregion

namespace Client.Interface
{
    /// <summary>
    ///     Simple client interface
    /// </summary>
    internal interface ICacheClient : IDisposable
    {
        ClusterInformation GetClusterInformation();

        ServerLog GetLog(int lastLines);

        /// <summary>
        ///     Set or reset the read-only mode
        /// </summary>
        /// <param name="rw"></param>
        void SetReadonlyMode(bool rw = false);


        /// <summary>
        ///     Delete all data
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
        ///     Add a new object or update an existent one.The object may be excluded from eviction.
        ///     This feature is useful for the loader components which pre load data into the cache
        /// </summary>
        /// <param name="item"></param>
        /// <param name="excludeFromEviction">if true, the item will never be automatically evicted </param>
        void Put<T>(T item, bool excludeFromEviction = false);

        /// <summary>
        ///     Transactionally add or replace a collection of objects of the same type
        ///     This method needs to lock the cache during the update. Do not use for large collections
        ///     (for example while pre loading data to a cache) Use
        ///     <see
        ///         cref="CacheClient.FeedMany{T}(System.Collections.Generic.IEnumerable{T},bool)" />
        ///     instead
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="items"></param>
        /// <param name="excludeFromEviction"></param>
        void PutMany<T>(IEnumerable<T> items, bool excludeFromEviction = false);

        /// <summary>
        ///     Add or replace a collection of cached objects. This method is not transactional. The
        ///     cache is fed by packet of <see cref="CacheClient.DefaultPacketSize" /> objects.
        ///     As we do not require a transactional update for the whole collection, the lock time is much smaller
        ///     Use this method when pre loading data to the cache. Use <see cref="CacheClient.PutMany{T}" /> when you need
        ///     transactional update for a small collection of objects
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="items"></param>
        /// <param name="excludeFromEviction"></param>
        void FeedMany<T>(IEnumerable<T> items, bool excludeFromEviction = false);

        /// <summary>
        ///     Add or replace a collection of cached objects. This method is not transactional. The
        ///     cache is fed by packet of <see cref="packetSize" /> objects.
        ///     As we do not require a transactional update for the whole collection the lock time is much smaller
        ///     Use this method when pre loading data to the cache. Use <see cref="CacheClient.PutMany{T}" /> when you need
        ///     transactional update for a small collection of objects
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="items"></param>
        /// <param name="excludeFromEviction"></param>
        /// <param name="packetSize"></param>
        void FeedMany<T>(IEnumerable<T> items, bool excludeFromEviction, int packetSize);

        /// <summary>
        ///     As an alternative to <see cref="CacheClient.FeedMany{T}(System.Collections.Generic.IEnumerable{T},bool,int)" /> you
        ///     can use
        ///     <see cref="CacheClient.BeginFeed{TItem}" /> <see cref="CacheClient.Add{TItem}" />
        ///     <see
        ///         cref="CacheClient.EndFeed{TItem}" />
        /// </summary>
        /// <typeparam name="TItem"></typeparam>
        /// <returns></returns>
        IFeedSession BeginFeed<TItem>(int packetSize, bool excludeFromEviction = true) where TItem : class;

        /// <summary>
        ///     As an alternative to <see cref="CacheClient.FeedMany{T}(System.Collections.Generic.IEnumerable{T},bool,int)" /> you
        ///     can use
        ///     <see cref="CacheClient.BeginFeed{TItem}" /> <see cref="CacheClient.Add{TItem}" />
        ///     <see
        ///         cref="CacheClient.EndFeed{TItem}" />
        /// </summary>
        /// <typeparam name="TItem"></typeparam>
        /// <param name="session"></param>
        /// <param name="item"></param>
        void Add<TItem>(IFeedSession session, TItem item) where TItem : class;

        /// <summary>
        ///     As an alternative to <see cref="CacheClient.FeedMany{T}(System.Collections.Generic.IEnumerable{T},bool,int)" /> you
        ///     can use
        ///     <see cref="CacheClient.BeginFeed{TItem}" /> <see cref="CacheClient.Add{TItem}" />
        ///     <see
        ///         cref="CacheClient.EndFeed{TItem}" />
        /// </summary>
        /// <typeparam name="TItem"></typeparam>
        /// <param name="session"></param>
        void EndFeed<TItem>(IFeedSession session) where TItem : class;

        /// <summary>
        ///     Remove all the items matching a query
        /// </summary>
        /// <param name="query"></param>
        /// <returns>number of items effectively removed</returns>
        int RemoveMany(OrQuery query);

        /// <summary>
        /// Compute a pivot table server-side
        /// </summary>
        /// <param name="filter">the items for which the pivot is computed</param>
        /// <param name="axis">ordered axis list for pivot calculation</param>
        /// <returns></returns>
        PivotLevel ComputePivot(OrQuery filter, params string[] axis);

        /// <summary>
        ///     Clears the cache for a given data type and also resets the hit ratio
        ///     It is much faster than <see cref="CacheClient.RemoveMany" /> with a query that matches all data
        /// </summary>
        /// <typeparam name="TItem"></typeparam>
        /// <returns></returns>
        int Truncate<TItem>();

        /// <summary>
        ///     Get one element by primary key
        /// </summary>
        /// <param name="value">value of the primary key to look for</param>
        /// <returns>the found object or null if none available</returns>
        TItemType GetOne<TItemType>(object value);

        /// <summary>
        ///     Get one element by unique key
        /// </summary>
        /// <param name="keyName">name of the unique key</param>
        /// <param name="value">value to look for</param>
        /// <returns>the found object or null if none available</returns>
        TItemType GetOne<TItemType>(string keyName, object value);

        /// <summary>
        ///     Retrieve multiple objects using a precompiled query.<br />
        ///     To create a valid <see cref="OrQuery" /> use a <see cref="QueryBuilder" />.
        /// </summary>
        /// <typeparam name="TItemType"></typeparam>
        /// <param name="query"></param>
        /// <returns></returns>
        IEnumerable<TItemType> GetMany<TItemType>(OrQuery query);
        
        /// <summary>
        ///     Remove an item from the cache.
        /// </summary>
        /// <typeparam name="TItemType"></typeparam>
        /// <param name="primaryKeyValue"></param>
        void Remove<TItemType>(object primaryKeyValue);


        /// <summary>
        ///     Mark a data type as fully loaded. GetMany requests may require that data is fully loaded
        /// </summary>
        /// <typeparam name="TItemType"></typeparam>
        /// <param name="isFullyLoaded"></param>
        void DeclareDataFullyLoaded<TItemType>(bool isFullyLoaded);


        /// <summary>
        ///     Check that data is fully loaded for the specified type
        /// </summary>
        /// <typeparam name="TItemType"></typeparam>
        /// <returns></returns>
        bool IsDataFullyLoaded<TItemType>();


        /// <summary>
        ///     Check if the server is up and running
        /// </summary>
        /// <returns></returns>
        bool Ping();

        /// <summary>
        ///     Explicitly registers a type on the client and the server. In order to use this version of the method
        ///     the type need to be tagged(attributes need to be associated to its public properties used as keys)
        ///     Explicit type registration is not required.
        ///     It is done automatically when the first Put() is executed for an item of the specified type
        ///     or when a query is first built
        ///     <br />
        ///     As type registration is an expensive operation, you may want to do it during the client initialization.
        /// </summary>
        /// <param name="type"></param>
        ClientSideTypeDescription RegisterTypeIfNeeded(Type type);

        /// <summary>
        ///     Register a type as cacheable with an external description
        ///     Cacheable type descriptions can be provided by attributes on the public properties
        ///     or as <see cref="TypeDescriptionConfig" />.
        ///     If an external description is required, the type must be explicitly registered.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="description"></param>
        ClientSideTypeDescription RegisterTypeIfNeeded(Type type, TypeDescriptionConfig description);

        /// <summary>
        /// Sent a message to the server to create a new empty indexed collection in none was already there for this type
        /// Used when the indexes are defined with tags on the type
        /// If <see cref="forceReindex"/> is true an existing collection will be re indexed according to the provided type description
        /// </summary>
        /// <param name="type"></param>
        /// <param name="forceReindex">force reindexing an existing collection</param>
        /// <returns></returns>
        ClientSideTypeDescription RegisterType(Type type, bool forceReindex = false);


        /// <summary>
        /// Sent a message to the server to create a new empty indexed collection in none was already there for this type
        /// Used when the indexes are defined in the xml configuration file
        /// If <see cref="forceReindex"/> is true an existing collection will be re indexed according to the provided type description
        /// </summary>
        /// <param name="type"></param>
        /// <param name="typeDescription">explicit type description</param>
        /// <param name="forceReindex">force reindexing an existing collection</param>
        /// <returns></returns>
        ClientSideTypeDescription RegisterType(Type type, ClientSideTypeDescription typeDescription, bool forceReindex = false);

        
        /// <summary>
        ///     Count the items matching the query and check for data completeness.
        /// </summary>
        /// <param name="query"></param>
        /// <returns>completeness, items count</returns>
        KeyValuePair<bool, int> EvalQuery(OrQuery query);

        /// <summary>
        ///     Dump all data to an external directory
        /// </summary>
        /// <param name="path"></param>
        void Dump(string path);

        /// <summary>
        ///     Reinitialize all data from a dump
        /// </summary>
        /// <param name="path"></param>
        void ImportDump(string path);

        /// <summary>
        ///     This is slower than ImportDump but it allows to change the number of nodes in the database
        /// </summary>
        void InitializeFromDump(string path);

        /// <summary>
        ///     Return cached object without deserialization to .NET object. Used to rocess data at json level
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        IList<CachedObject> GetObjectDescriptions(OrQuery query);

        /// <summary>
        ///     Stop or restart all the servers in the cluster
        /// </summary>
        /// <param name="restart"></param>
        void Stop(bool restart);


        /// <summary>
        ///     Put ans delete items in an ACID transaction
        /// </summary>
        /// <param name="itemsToPut"></param>
        /// <param name="conditions">
        ///     A list of conditions for conditional update. Needs to have the same size as itemsToPut. May
        ///     contain null values
        /// </param>
        /// <param name="itemsToDelete"></param>
        void ExecuteTransaction(IList<CachedObject> itemsToPut, IList<OrQuery> conditions,
            IList<CachedObject> itemsToDelete = null);

        void Import(string jsonFile);

        bool TryAdd<T>(T item);

        void UpdateIf<T>(T newValue, OrQuery testAsQuery);

        #region cache only methods

        /// <summary>
        ///     Declare a subset of data as being fully available in the cache.<br />
        ///     Used by loader components to declare data preloaded in the cache.
        ///     <seealso cref="DomainDescription" />
        /// </summary>
        /// <param name="domain">data description</param>
        void DeclareDomain(DomainDescription domain);


        /// <summary>
        ///     Activate eviction for a type name. If EvictionType.Lru is used when the limit is reached
        ///     the less recently used <paramref name="itemsToRemove" /> items will be evicted
        /// </summary>
        /// <param name="fullTypeName"></param>
        /// <param name="evictionType"></param>
        /// <param name="limit"></param>
        /// <param name="itemsToRemove"></param>
        /// <param name="timeLimitInMilliseconds"></param>
        void ConfigEviction(string fullTypeName, EvictionType evictionType, int limit, int itemsToRemove, int timeLimitInMilliseconds);

        #endregion
    }
}