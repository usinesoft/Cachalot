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
        /// <summary>
        ///     Create an empty query (called internally by the query builder)
        /// </summary>
        public AndQuery()
        {
            Elements = new List<AtomicQuery>();
        }

        
        public override bool IsValid
        {
            get { return Elements.All(atomicQuery => atomicQuery.IsValid); }
        }

        /// <summary>
        ///     Accessor for the underlying elements (<see cref="AtomicQuery" />
        /// </summary>
        [field: ProtoMember(1)]
        public List<AtomicQuery> Elements { get; private set; }

        public AndQuery Clone()
        {
            return new AndQuery {Elements = new List<AtomicQuery>(Elements.Select(e => e.Clone()))};
        }


        public override string ToString()
        {
            if (Elements.Count == 0)
                return "<empty>";
            if (Elements.Count == 1)
                return Elements[0].ToString();

            var sb = new StringBuilder();
            for (var i = 0; i < Elements.Count; i++)
            {
                sb.Append(Elements[i]);
                if (i != Elements.Count - 1)
                    sb.Append(" AND ");
            }

            return sb.ToString().Trim();
        }


        public override bool Match(CachedObject item)
        {
            return Elements.All(t => t.Match(item));
        }

        public bool IsSubsetOf(AndQuery query)
        {
            return query.Elements.All(e => Elements.Any(q => q.IsSubsetOf(e)));
        }
    }
}