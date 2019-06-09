#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Client.Core;
using Client.Messages;
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
        [ProtoMember(1)] private readonly List<AndQuery> _elements;

        /// <summary>
        ///     Create an empty query (called internally by the query builder)
        /// </summary>
        public OrQuery(Type type)
        {
            TypeName = type.FullName;
            _elements = new List<AndQuery>();
        }


        public OrQuery(string typeName)
        {
            TypeName = typeName;
            _elements = new List<AndQuery>();
        }


        public OrQuery(TypeDescription typeDescription)
        {
            TypeName = typeDescription.FullTypeName;
            _elements = new List<AndQuery>();
        }

        /// <summary>
        ///     For protobuf serialization only
        /// </summary>
        public OrQuery()
        {
            _elements = new List<AndQuery>();
        }

        public override bool IsValid
        {
            get { return Elements.All(element => element.IsValid); }
        }

        /// <summary>
        ///     The elements of type <see cref="AndQuery" />
        /// </summary>
        public IList<AndQuery> Elements => _elements;

        [field: ProtoMember(2)] public string TypeName { get; }

        public bool MultipleWhereClauses { get; set; }
        [ProtoMember(3)] public int Take { get; set; }
        public bool CountOnly { get; set; }
        public int Skip { get; set; }

        [ProtoMember(4)] public string FullTextSearch { get; set; }
        [ProtoMember(5)] public bool OnlyIfComplete { get; set; }


        public bool IsFullTextQuery => !string.IsNullOrWhiteSpace(FullTextSearch);

        public static OrQuery Empty<T>()
        {
            return new OrQuery(typeof(T).FullName);
        }

        public bool IsSubsetOf(OrQuery query)
        {
            if (query.IsEmpty()) return true;

            return _elements.All(q => query.Elements.Any(q.IsSubsetOf));
        }

        public override string ToString()
        {
            if (_elements.Count == 0)
                return "<empty>";
            if (_elements.Count == 1)
            {
                var result = _elements[0].ToString();
                if (!string.IsNullOrWhiteSpace(FullTextSearch)) result += $" + Full text search ({FullTextSearch})";

                if (OnlyIfComplete)
                    result += " + Only if complete ";

                return result;
            }

            var sb = new StringBuilder();
            for (var i = 0; i < _elements.Count; i++)
            {
                sb.Append(_elements[i]);
                if (i != _elements.Count - 1)
                    sb.Append(" OR");
            }

            if (!string.IsNullOrWhiteSpace(FullTextSearch)) sb.Append($" + Full text search ({FullTextSearch})");

            if (OnlyIfComplete)
                sb.Append(" + Only if complete ");

            return sb.ToString();
        }


        public override bool Match(CachedObject item)
        {
            return Elements.Any(t => t.Match(item));
        }

        public bool IsEmpty()
        {
            return Elements.Count == 0;
        }
    }
}