using Client.Core;
using ProtoBuf;

namespace Client.Queries
{
    /// <summary>
    ///     Abstract base class for the queries
    /// </summary>
    [ProtoContract]
    [ProtoInclude(500, typeof(AtomicQuery))]
    [ProtoInclude(501, typeof(AndQuery))]
    [ProtoInclude(502, typeof(OrQuery))]
    public abstract class Query
    {
        /// <summary>
        ///     Check if the current query is valid. Validity rules are specific to each subclass
        /// </summary>
        public abstract bool IsValid { get; }

        /// <summary>
        ///     Return true if the current query matches the specified object
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public abstract bool Match(CachedObject item);
    }
}