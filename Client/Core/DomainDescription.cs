using System;
using System.Collections.Generic;
using Client.Interface;
using Client.Queries;
using ProtoBuf;

namespace Client.Core
{
    /// <summary>
    ///     Describes the data which is preloaded into the cache.<br />
    ///     The domain description is attached to a data type.
    ///     Preloaded data is either defined as complete (if <see pa="IsFullyLoaded" /> is true all the available objects are
    ///     preloaded)
    ///     or described by a sequence of <see cref="AtomicQuery" />
    ///     <example>
    ///         If the preloaded data is described by [Folder = "FFF", ValueDate > 20010101] for the data type Trade, all the
    ///         trades having
    ///         Folder = "FFF" are preloaded AND all trades having ValueDate > 20010101 are ALSO preloaded.<br />
    ///         The preloaded data is the UNION of data matching each atomic query
    ///     </example>
    /// </summary>
    [ProtoContract]
    [Serializable]
    public class DomainDescription
    {
        /// <summary>
        ///     The data corresponding to each <see cref="KeyValue" /> is fully available in the cache
        ///     Indexed by key name to speed up the evaluation of query completeness and indexed by <see cref="KeyValue" />
        ///     to speed up Domain merge and subtraction
        /// </summary>
        [ProtoMember(2)] private readonly Dictionary<string, Dictionary<KeyValue, AtomicQuery>> _completeQueriesByKey;

        [ProtoMember(1)] private readonly string _fullTypeName;

        /// <summary>
        ///     Optional user readable domain description
        /// </summary>
        [ProtoMember(4)] private string _description;


        /// <summary>
        ///     If true all instances of the current type are loaded
        /// </summary>
        [ProtoMember(3)] private bool _isFullyLoaded;

        /// <summary>
        ///     For serialization only
        /// </summary>
        public DomainDescription()
        {
        }

        /// <summary>
        ///     Public constructor (creates an empty description for the specified type)
        /// </summary>
        public DomainDescription(Type type)
        {
            _fullTypeName = type.FullName;
            _completeQueriesByKey = new Dictionary<string, Dictionary<KeyValue, AtomicQuery>>();
        }

        /// <summary>
        ///     Public constructor (creates an empty description for the specified type)
        /// </summary>
        public DomainDescription(string fullTypeName)
        {
            _fullTypeName = fullTypeName;
            _completeQueriesByKey = new Dictionary<string, Dictionary<KeyValue, AtomicQuery>>();
        }

        /// <summary>
        ///     The data coresponding to each <see cref="AtomicQuery" /> is fully available in the cache
        /// </summary>
        public IEnumerable<AtomicQuery> CompleteQueries
        {
            get
            {
                var result = new List<AtomicQuery>();

                if (_completeQueriesByKey != null
                ) // protobuf serializer does not make any distinction between null and empty collections
                    foreach (
                        var keyValuePair in _completeQueriesByKey)
                        result.AddRange(keyValuePair.Value.Values);

                return result;
            }
        }


        /// <summary>
        ///     Optional user readable domain description
        /// </summary>
        public string Description
        {
            get => _description;
            set => _description = value;
        }

        /// <summary>
        ///     If true all instances of the current type are loaded
        /// </summary>
        public bool IsFullyLoaded
        {
            get => _isFullyLoaded;
            set
            {
                if (value & (_completeQueriesByKey.Count > 0))
                    throw new CacheException(
                        "A domain is either complete or described by a sequence of atomic queries, not both");

                _isFullyLoaded = value;
            }
        }

        /// <summary>
        ///     The full name of the associated data type
        /// </summary>
        public string FullTypeName => _fullTypeName;

        /// <summary>
        ///     Get all the queries attached to a specified key name
        /// </summary>
        /// <param name="keyName"></param>
        /// <returns></returns>
        public IList<AtomicQuery> GetCompleteQueriesByKey(string keyName)
        {
            if (_completeQueriesByKey.ContainsKey(keyName))
                return new List<AtomicQuery>(_completeQueriesByKey[keyName].Values);

            return new List<AtomicQuery>(); //return empty list
        }


        /// <summary>
        ///     Merge the current domain with the specified one
        /// </summary>
        /// <param name="toAdd"></param>
        public void Add(DomainDescription toAdd)
        {
            if (toAdd.IsFullyLoaded)
            {
                IsFullyLoaded = true;
                return;
            }

            foreach (var query in toAdd.CompleteQueries) AddOrReplace(query);
        }

        /// <summary>
        ///     Remove the completeness definitions contained in another domain description from the current one
        /// </summary>
        /// <param name="toAdd"></param>
        public void Remove(DomainDescription toAdd)
        {
            if (toAdd.IsFullyLoaded)
            {
                IsFullyLoaded = false;
                return;
            }

            foreach (var query in toAdd.CompleteQueries) remove(query);
        }

        /// <summary>
        ///     Remove a query from the domain definition
        /// </summary>
        /// <param name="queryToRemove"></param>
        private void remove(AtomicQuery queryToRemove)
        {
            var keyName = queryToRemove.Value.KeyName;
            if (_completeQueriesByKey.ContainsKey(keyName))
            {
                var dict = _completeQueriesByKey[keyName];
                if (dict.ContainsKey(queryToRemove.Value))
                    dict.Remove(queryToRemove.Value);
            }
        }


        public override string ToString()
        {
            var result = string.Format("All data preloaded = {0}", _isFullyLoaded);
            result += ": prealoaded data = ";
            foreach (var completeQuery in CompleteQueries)
            {
                result += completeQuery.ToString();
                result += "|";
            }

            return result;
        }


        /// <summary>
        ///     Add an <see cref="AtomicQuery" /> to domain description
        ///     Only one query by <see cref="KeyValue" /> is allowed.If a query on the same <see cref="KeyValue" />is already
        ///     present in the domain
        ///     it is replaced
        /// </summary>
        /// <param name="query"></param>
        public void AddOrReplace(AtomicQuery query)
        {
            if (_isFullyLoaded)
                throw new CacheException(
                    "A domain is either complete or described by a sequence of atomic queries, not both");

            var keyName = query.Value.KeyName;
            Dictionary<KeyValue, AtomicQuery> queries;
            if (_completeQueriesByKey.ContainsKey(keyName))
            {
                queries = _completeQueriesByKey[keyName];
            }
            else
            {
                queries = new Dictionary<KeyValue, AtomicQuery>();
                _completeQueriesByKey.Add(keyName, queries);
            }

            queries[query.Value] = query;
        }
    }
}