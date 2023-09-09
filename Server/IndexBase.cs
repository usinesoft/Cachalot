#region

using System;
using System.Collections.Generic;
using Client.Core;
using Client.Messages;
using Client.Queries;

#endregion

namespace Server;

/// <summary>
///     Base class for all indices
///     Al support Put, Remove and GetMany
///     Only ordered indices support operators others than == (Eq or In)
/// </summary>
public abstract class IndexBase : IReadOnlyIndex
{
    protected IndexBase(KeyInfo keyInfo)
    {
        KeyInfo = keyInfo;
    }

    protected KeyInfo KeyInfo { get; }

    public string Name => KeyInfo.Name;

    public abstract IndexType IndexType { get; }

    /// <summary>
    ///     value Eq (or In ) is valid far all the indices
    ///     the others (Le,Lt,Ge,Gt) only for ordered ones (<cref>IsOrdered</cref> = true
    /// </summary>
    /// <param name="values"> one value for equality operator or multiple values for In operator</param>
    /// <param name="op"></param>
    /// <returns></returns>
    public abstract ISet<PackedObject> GetMany(IList<KeyValue> values, QueryOperator op = QueryOperator.Eq);

    public virtual IEnumerable<PackedObject> GetAll(bool descendingOrder = false, int maxCount = 0)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    ///     Count the items from the index matching the criteria
    /// </summary>
    /// <param name="values">value(s) to search for</param>
    /// <param name="op">operator to apply to the value</param>
    /// <param name="fastEstimate">quick estimation used for planning (makes a difference only for ordered indexes)</param>
    /// <returns>number of elements matching the key values and the operator or int.MaxValue</returns>
    public abstract int GetCount(IList<KeyValue> values, QueryOperator op = QueryOperator.Eq,
                                 bool fastEstimate = false);

    public abstract void BeginFill();

    /// <summary>
    ///     Put a new item in the index
    ///     REQUIRE: no item having the same primary key exists in the index
    ///     If an item
    /// </summary>
    /// <param name="item"></param>
    public abstract void Put(PackedObject item);

    public abstract void EndFill();


    public abstract void RemoveOne(PackedObject item);

    public abstract void Clear();
    public abstract void RemoveMany(IList<PackedObject> items);
}