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
    ///     A list of and queries bound by an OR operator
    /// </summary>
    [ProtoContract]
    public class OrQuery : Query
    {
        #region persistent properties

        /// <summary>
        /// An OrQuery is a list of AndQueries.If the list is empty then the query matches all the items in the collection
        /// or it is a pure full-text query (if <see cref="FullTextSearch"/> is not empty)
        /// </summary>
        [field: ProtoMember(1)] public List<AndQuery> Elements { get; } = new List<AndQuery>();
        

        /// <summary>
        /// Any query applies to exactly one collection
        /// </summary>
        [field: ProtoMember(2)] public string CollectionName { get; set; }

        /// <summary>
        /// Skip operator (ignore the first elements)
        /// </summary>
        [field: ProtoMember(3)]public int Skip { get; set; }

        /// <summary>
        /// Take operator (only take the first elements)
        /// </summary>
        [field:ProtoMember(4)] public int Take { get; set; }

        /// <summary>
        /// Full text query (optional)
        /// </summary>
        [field:ProtoMember(5)] public string FullTextSearch { get; set; }

        /// <summary>
        /// Distinct operator. Can be applied only with Select clause
        /// </summary>
        [field:ProtoMember(6)] public bool Distinct { get; set; }
        
        /// <summary>
        /// Properties in the Select clause. If empty, the complete object is returned
        /// </summary>
        [field:ProtoMember(7)] public IList<SelectItem>  SelectClause{ get; } = new List<SelectItem>();

        /// <summary>
        /// Specific operator for cache-only mode; returns a result only if the query is a subset of the domain loaded into the cache
        /// </summary>
        [field:ProtoMember(8)] public bool OnlyIfComplete { get; set; }

        /// <summary>
        /// Optional order-by clause. Only one accepted in this version
        /// </summary>
        [field:ProtoMember(9)] public string OrderByProperty { get; set; }
        
        /// <summary>
        /// True for descending order (only applies if <see cref="OrderByProperty"/> is present
        /// </summary>
        [field:ProtoMember(10)] public bool OrderByIsDescending { get; set; }

       
        #endregion

        /// <summary>
        /// Only evaluate the query. Do not return elements
        /// </summary>
        public bool CountOnly { get; set; }

        /// <summary>
        /// Non persistent, used during construction
        /// </summary>
        public bool MultipleWhereClauses { get; set; }

        #region interface implementation

        public override bool IsValid
        {
            get { return Elements.All(element => element.IsValid); }
        }

        public override string ToString()
        {
            if (Elements.Count == 0)
                return "<empty>";
            if (Elements.Count == 1)
            {
                var result = Elements[0].ToString();
                if (!string.IsNullOrWhiteSpace(FullTextSearch)) result += $" + Full text search ({FullTextSearch})";

                if (OnlyIfComplete)
                    result += " + Only if complete ";

                return result;
            }

            var sb = new StringBuilder();
            for (var i = 0; i < Elements.Count; i++)
            {
                sb.Append(Elements[i]);
                if (i != Elements.Count - 1)
                    sb.Append(" OR ");
            }

            if (!string.IsNullOrWhiteSpace(FullTextSearch)) sb.Append($" + Full text search ({FullTextSearch})");

            if (OnlyIfComplete)
                sb.Append(" + Only if complete ");

            return sb.ToString();
        }

        /// <summary>
        /// Returns true if the objects matches the query
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public override bool Match(PackedObject item)
        {
            return Elements.Any(t => t.Match(item));
        }

        #endregion

        public bool IsEmpty()
        {
            return Elements.Count == 0;
        }


        public bool IsFullTextQuery => !string.IsNullOrWhiteSpace(FullTextSearch);

        public OrQuery(string collectionName)
        {
            CollectionName = collectionName;
        }


        /// <summary>
        ///  For protobuf serialization only
        /// </summary>
        public OrQuery()
        {
            
        }

        public static OrQuery Empty<T>()
        {
            return new OrQuery(typeof(T).FullName);
        }

        public static OrQuery Empty(string collectionName)
        {
            return new OrQuery(collectionName);
        }

        public bool IsSubsetOf(OrQuery query)
        {
            if (query.IsEmpty()) return true;

            return Elements.All(q => query.Elements.Any(q.IsSubsetOf));
        }
    }


    /// <summary>
    /// One column (property) from a select clause
    /// </summary>
    [ProtoContract]
    public class SelectItem
    {
        [ProtoMember(1)] public string Name { get; set; }
        [ProtoMember(2)] public string Alias { get; set; }

    }
}