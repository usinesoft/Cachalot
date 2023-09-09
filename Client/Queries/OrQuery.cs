#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Client.Core;
using JetBrains.Annotations;
using ProtoBuf;

#endregion

namespace Client.Queries;

/// <summary>
///     A list of and queries bound by an OR operator
/// </summary>
[ProtoContract]
public class OrQuery : Query
{
    public OrQuery(string collectionName)
    {
        CollectionName = collectionName;
    }


    /// <summary>
    ///     For protobuf serialization only
    /// </summary>
    [UsedImplicitly]
    public OrQuery()
    {
    }

    /// <summary>
    ///     Only evaluate the query. Do not return elements
    /// </summary>
    public bool CountOnly { get; set; }

    /// <summary>
    ///     Non persistent, used during construction
    /// </summary>
    public bool MultipleWhereClauses { get; set; }


    public bool IsFullTextQuery => !string.IsNullOrWhiteSpace(FullTextSearch);

    public bool IsEmpty()
    {
        return Elements.Count == 0;
    }

    public new static OrQuery Empty<T>()
    {
        return new(typeof(T).Name);
    }

    public new static OrQuery Empty(string collectionName)
    {
        return new(collectionName);
    }

    public bool IsSubsetOf(OrQuery query)
    {
        if (query.IsEmpty()) return true;

        return Elements.All(q => query.Elements.Any(q.IsSubsetOf));
    }

    #region persistent properties

    /// <summary>
    ///     An OrQuery is a list of AndQueries.If the list is empty then the query matches all the items in the collection
    ///     or it is a pure full-text query (if <see cref="FullTextSearch" /> is not empty)
    /// </summary>
    [field: ProtoMember(1)]
    public List<AndQuery> Elements { get; } = new();


    /// <summary>
    ///     Any query applies to exactly one collection
    /// </summary>
    [field: ProtoMember(2)]
    public string CollectionName { get; set; }

    /// <summary>
    ///     Skip operator (ignore the first elements)
    /// </summary>
    [field: ProtoMember(3)]
    public int Skip { get; set; }

    /// <summary>
    ///     Take operator (only take the first elements)
    /// </summary>
    [field: ProtoMember(4)]
    public int Take { get; set; }

    /// <summary>
    ///     Full text query (optional)
    /// </summary>
    [field: ProtoMember(5)]
    public string FullTextSearch { get; set; }

    /// <summary>
    ///     Distinct operator. Can be applied only with Select clause
    /// </summary>
    [field: ProtoMember(6)]
    public bool Distinct { get; set; }

    /// <summary>
    ///     Properties in the Select clause. If empty, the complete object is returned
    /// </summary>
    [field: ProtoMember(7)]
    public IList<SelectItem> SelectClause { get; } = new List<SelectItem>();

    /// <summary>
    ///     Specific operator for cache-only mode; returns a result only if the query is a subset of the domain loaded into the
    ///     cache
    /// </summary>
    [field: ProtoMember(8)]
    public bool OnlyIfComplete { get; set; }

    /// <summary>
    ///     Optional order-by clause. Only one accepted in this version
    /// </summary>
    [field: ProtoMember(9)]
    public string OrderByProperty { get; set; }

    /// <summary>
    ///     True for descending order (only applies if <see cref="OrderByProperty" /> is present
    /// </summary>
    [field: ProtoMember(10)]
    public bool OrderByIsDescending { get; set; }

    /// <summary>
    ///     Simple query. Search one element by primary key
    /// </summary>
    [field: ProtoMember(11)]
    public bool ByPrimaryKey { get; set; }

    /// <summary>
    ///     Optional unique id that can be used to trace the query in the activity log
    /// </summary>
    [field: ProtoMember(12)]
    public Guid QueryId { get; set; }

    #endregion

    #region interface implementation

    public override bool IsValid
    {
        get { return Elements.All(element => element.IsValid); }
    }

    /// <summary>
    ///     Generate the part before WHERE
    /// </summary>
    /// <param name="sb"></param>
    private void GeneratePrefix(StringBuilder sb)
    {
        sb.Append(CountOnly ? "COUNT" : "SELECT");

        sb.Append(" ");

        if (Distinct) sb.Append("DISTINCT");

        sb.Append(" ");

        // the projection clause
        if (SelectClause.Count > 0)
        {
            sb.Append(string.Join(',', SelectClause.Select(x => x.Name)));
            sb.Append(" ");
        }

        sb.Append("FROM");
        sb.Append(" ");

        sb.Append(CollectionName);
        sb.Append(" ");

        if (Elements.Count > 0)
        {
            sb.Append("WHERE");
            sb.Append(" ");
        }
    }

    /// <summary>
    ///     Write extra information at the end: like full-text search query etc..
    /// </summary>
    /// <param name="sb"></param>
    private void GenerateSuffix(StringBuilder sb)
    {
        if (!string.IsNullOrWhiteSpace(OrderByProperty))
        {
            sb.Append(" ");
            sb.Append("ORDER BY");
            sb.Append(" ");
            sb.Append(OrderByProperty);

            if (OrderByIsDescending)
            {
                sb.Append(" ");
                sb.Append("DESCENDING");
            }
        }

        if (!string.IsNullOrWhiteSpace(FullTextSearch)) sb.Append($" + Full text search ({FullTextSearch})");

        if (OnlyIfComplete) sb.Append(" + Only if complete");
    }

    public override string ToString()
    {
        var sb = new StringBuilder();

        GeneratePrefix(sb);

        for (var i = 0; i < Elements.Count; i++)
        {
            sb.Append(Elements[i]);
            if (i != Elements.Count - 1)
                sb.Append(" OR ");
        }

        GenerateSuffix(sb);

        return sb.ToString();
    }

    /// <summary>
    ///     Like <see cref="ToString" /> but without the parameter values
    ///     To be used for administrative tasks like detecting the most time-consuming type of query
    /// </summary>
    /// <returns></returns>
    public string Description()
    {
        var sb = new StringBuilder();
        GeneratePrefix(sb);

        for (var i = 0; i < Elements.Count; i++)
        {
            sb.Append(Elements[i].Description());
            if (i != Elements.Count - 1)
                sb.Append(" OR ");
        }

        GenerateSuffix(sb);

        return sb.ToString();
    }

    /// <summary>
    ///     Returns true if the objects matches the query
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public override bool Match(PackedObject item)
    {
        // an empty query matches everything
        return IsEmpty() || Elements.Any(t => t.Match(item));
    }

    #endregion
}

/// <summary>
///     One column (property) from a select clause
/// </summary>
[ProtoContract]
public class SelectItem
{
    [ProtoMember(1)] public string Name { get; set; }
    [ProtoMember(2)] public string Alias { get; set; }
}