#region

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Client.Core;
using ProtoBuf;

#endregion

namespace Client.Queries
{
    /// <summary>
    ///     A list of atomic queries bound by an AND operator
    /// </summary>
    [ProtoContract]
    public class AndQuery : Query
    {
        [ProtoMember(1)] private List<AtomicQuery> _elements;

        /// <summary>
        ///     Create an empty query (called internally by the query builder)
        /// </summary>
        public AndQuery()
        {
            _elements = new List<AtomicQuery>();
        }

        /// <summary>
        ///     The contained atomic queries should apply to different keys
        /// </summary>
        public override bool IsValid
        {
            get { return Elements.All(atomicQuery => atomicQuery.IsValid); }
        }

        /// <summary>
        ///     Accessor for the underlying elements (<see cref="AtomicQuery" />
        /// </summary>
        public List<AtomicQuery> Elements => _elements;

        public AndQuery Clone()
        {
            return new AndQuery {_elements = new List<AtomicQuery>(Elements.Select(e => e.Clone()))};
        }

        /// <summary>
        ///     This query is a subset of a domain if at least one of the contained atomic queries is a subset
        /// </summary>
        /// <param name="domain"></param>
        /// <returns></returns>
        public bool IsSubsetOf(DomainDescription domain)
        {
            if (domain.IsFullyLoaded)
                return true;

            return _elements.Any(query => query.IsSubsetOf(domain));
        }


        public override string ToString()
        {
            if (_elements.Count == 0)
                return "<empty>";
            if (_elements.Count == 1)
                return _elements[0].ToString();

            var sb = new StringBuilder();
            for (var i = 0; i < _elements.Count; i++)
            {
                sb.Append(_elements[i]);
                if (i != _elements.Count - 1)
                    sb.Append(" AND ");
            }

            return sb.ToString();
        }


        public override bool Match(CachedObject item)
        {
            return Elements.All(t => t.Match(item));
        }
    }
}