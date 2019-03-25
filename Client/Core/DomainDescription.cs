using System;
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
        ///     For serialization only
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public DomainDescription()
        {
        }


        /// <summary>
        /// If an empty query is passed then all the data is available in the cache
        /// If a null query is passed then no domain description is available
        /// </summary>
        /// <param name="descriptionAsQuery"></param>
        /// <param name="isFullyLoaded">if true all data is available</param>
        /// <param name="description"></param>
        public DomainDescription(OrQuery descriptionAsQuery, bool isFullyLoaded = false, string description = null)
        {
            DescriptionAsQuery = descriptionAsQuery;
            IsFullyLoaded = isFullyLoaded;
            Description = description;
        }

        


        /// <summary>
        ///     The data corresponding to each <see cref="KeyValue" /> is fully available in the cache
        ///     Indexed by key name to speed up the evaluation of query completeness and indexed by <see cref="KeyValue" />
        ///     to speed up Domain merge and subtraction
        /// </summary>
        [ProtoMember(1)]
        public OrQuery DescriptionAsQuery { get; }



        /// <summary>
        ///     Optional user readable domain description
        /// </summary>
        [ProtoMember(2)]
        private string Description { get; }

        

        /// <summary>
        ///     Optional user readable domain description
        /// </summary>
        [ProtoMember(3)]
        public bool IsFullyLoaded { get; }


        public override string ToString()
        {
            return IsFullyLoaded
                ? "All data preloaded "
                : "preloaded data:" + (Description ?? DescriptionAsQuery.ToString());
        }
    }
}