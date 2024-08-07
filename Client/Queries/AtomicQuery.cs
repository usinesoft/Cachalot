using System;
using System.Collections.Generic;
using System.Linq;
using Client.Core;
using Client.Messages;
using JetBrains.Annotations;
using ProtoBuf;

namespace Client.Queries;

/// <summary>
///     Just an operator and one or two values.
///     This class is immutable.
///     The second value is useful only for GeLe (between) operator
/// </summary>
[ProtoContract]
public sealed class AtomicQuery : Query, IEquatable<AtomicQuery>
{
    /// <summary>
    ///     Parameter-less constructor used for serialization
    /// </summary>
    [UsedImplicitly]
    private AtomicQuery()
    {
    }


    /// <summary>
    ///     Build a simple atomic query (one value and unary operator)
    /// </summary>
    /// <param name="metadata">The metadata of the value</param>
    /// <param name="value"></param>
    /// <param name="oper"></param>
    public AtomicQuery([NotNull] KeyInfo metadata, [NotNull] KeyValue value, QueryOperator oper = QueryOperator.Eq)
    {
        if (metadata == null) throw new ArgumentNullException(nameof(metadata));

        if (value == null) throw new ArgumentNullException(nameof(value));

        // only scalar operators can be used here
        if (oper is QueryOperator.In or QueryOperator.NotIn || oper.IsRangeOperator())
            throw new ArgumentException("invalid operator");


        Metadata = metadata;
        Value = value;
        Operator = oper;
    }


    /// <summary>
    ///     Build an IN or NOT IN query
    /// </summary>
    /// <param name="metadata"></param>
    /// <param name="values"></param>
    /// <param name="oper">may be <see cref="QueryOperator.In" /> or <see cref="QueryOperator.NotIn" /></param>
    public AtomicQuery([NotNull] KeyInfo metadata, [NotNull] ICollection<KeyValue> values,
                       QueryOperator oper = QueryOperator.In)
    {
        if (metadata == null) throw new ArgumentNullException(nameof(metadata));
        if (values == null) throw new ArgumentNullException(nameof(values));

        if (metadata.IsCollection)
            throw new ArgumentException("An IN (or NOT IN) query applies to a scalar value");

        if (oper != QueryOperator.In && oper != QueryOperator.NotIn)
            throw new ArgumentException("invalid operator");


        Metadata = metadata;
        _inValues = new(values);
        Operator = oper;
    }

    /// <summary>
    ///     Build a query of the type BETWEEN value value2
    /// </summary>
    /// <param name="metadata"></param>
    /// <param name="value"></param>
    /// <param name="value2"></param>
    /// <param name="oper">A range operator like <see cref="QueryOperator.GeLe" /></param>
    public AtomicQuery([NotNull] KeyInfo metadata, [NotNull] KeyValue value, [NotNull] KeyValue value2,
                       QueryOperator oper = QueryOperator.GeLe)
    {
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Value2 = value2 ?? throw new ArgumentNullException(nameof(value2));

        if (!oper.IsRangeOperator())
            throw new ArgumentException("Only range operators can be used with two values");


        Operator = oper; //the one and only binary operator
    }

    public IndexType IndexType => Metadata.IndexType;

    public string PropertyName => Metadata.Name;


    /// <summary>
    ///     Check if the query is valid
    /// </summary>
    public override bool IsValid
    {
        get
        {
            // BETWEEN requires two values
            if (Operator.IsRangeOperator())
            {
                if (ReferenceEquals(Value, null))
                    return false;

                if (ReferenceEquals(Value2, null))
                    return false;

                if (Value2 < Value)
                    return false;
            }


            // two values are valid only for range operators
            if (Value2 is not null && !Operator.IsRangeOperator()) return false;

            switch (Operator)
            {
                // IN requires a list of values
                case QueryOperator.In when InValues.Count == 0:
                // NOT IN requires a list of values
                case QueryOperator.NotIn when InValues.Count == 0:
                    return false;
            }

            // only IN and NOT IN accept a list of values
            if (Operator != QueryOperator.In && Operator != QueryOperator.NotIn && InValues.Count > 0)
                return false;

            // any operator except IN and NOT IN requires Value to be filled
            if (Operator != QueryOperator.In && Operator != QueryOperator.NotIn && ReferenceEquals(Value, null))
                return false;


            return true;
        }
    }

    public override bool IsEmpty()
    {
        return false;
    }

    public ICollection<KeyValue> InValues => _inValues;

    public IList<KeyValue> GetValues()
    {
        if (_inValues.Count > 0)
        {
            return _inValues.ToList();
        }
        
        return Value2 is not null
                ? new() { Value, Value2 }
                : new List<KeyValue> { Value };
    }

    public bool IsComparison =>
        Operator is QueryOperator.Eq or QueryOperator.Le or QueryOperator.Lt or QueryOperator.Ge or QueryOperator.Gt ||
        Operator.IsRangeOperator();

    public bool Equals(AtomicQuery right)
    {
        if (right == null) return false;

        if (!Equals(Operator, right.Operator)) return false;

        if (Metadata.Name != right.Metadata.Name) return false;

        if (Operator.IsRangeOperator())
            return Value == right.Value && Value2 == right.Value2;

        if (Operator == QueryOperator.In || Operator == QueryOperator.NotIn)
        {
            if (InValues.Count != right.InValues.Count) return false;

            var myValues = _inValues.ToList();

            var rightValues = right._inValues.ToList();

            for (var i = 0; i < myValues.Count; i++)
                if (myValues[i] != rightValues[i])
                    return false;

            return true;
        }

        if (Value != right.Value)
            return false;


        return true;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        return Equals(obj as AtomicQuery);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            if (Operator.IsRangeOperator())
                return Value.GetHashCode() + Value2.GetHashCode() + Operator.GetHashCode();

            if (Operator != QueryOperator.In && Operator != QueryOperator.NotIn)
                return Value.GetHashCode() + Operator.GetHashCode();

            var sum = 0;
            foreach (var value in InValues)
                sum += value.GetHashCode();

            return Operator.GetHashCode() + sum;
        }
    }

    /// <summary>
    ///     Check if the operators are compatible to define a subset relationship
    ///     Non trivial compatibility cases:
    ///     a &lt; b is subset of a &lt;= b
    ///     a &gt; b is subset of a &gt;= b
    ///     a = b is subset of a oper c if b oper c
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    private static bool AreOperatorsCompatibleWithSubset(QueryOperator left, QueryOperator right)
    {
        if (left == right)
            return true;

        if (left.IsRangeOperator() && right.IsRangeOperator())
            return true;

        if (left == QueryOperator.NotContains || left == QueryOperator.NotEq || left == QueryOperator.StrContains ||
            left == QueryOperator.StrEndsWith || left == QueryOperator.StrStartsWith)
            return false;

        if (right == QueryOperator.NotContains || right == QueryOperator.NotEq || right == QueryOperator.StrContains ||
            right == QueryOperator.StrEndsWith || right == QueryOperator.StrStartsWith)
            return false;


        if (right == QueryOperator.Eq)
            return false;

        if (left == QueryOperator.Eq)
            return true;

        if (left == QueryOperator.Gt && right == QueryOperator.Ge)
            return true;

        if (left == QueryOperator.Lt && right == QueryOperator.Le)
            return true;

        return false;
    }


    /// <summary>
    ///     Check if this atomic query is a subset of another atomic query
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    public bool IsSubsetOf(AtomicQuery query)
    {
        var rightOperator = query.Operator;

        if (PropertyName != query.PropertyName)
            return false;

        Dbg.CheckThat(Value.Type == query.Value.Type);

        if (!AreOperatorsCompatibleWithSubset(Operator, rightOperator))
            return false;


        switch (Operator)
        {
            case QueryOperator.Eq:
                if (rightOperator == QueryOperator.Eq) return Value == query.Value;
                if (rightOperator == QueryOperator.Le) return Value <= query.Value;
                if (rightOperator == QueryOperator.Lt) return Value < query.Value;
                if (rightOperator == QueryOperator.Ge) return Value >= query.Value;
                if (rightOperator == QueryOperator.Gt) return Value > query.Value;
                return false;

            case QueryOperator.Le:
                return Value <= query.Value;

            case QueryOperator.Lt:
                return Value <= query.Value;

            case QueryOperator.Gt:
                return Value >= query.Value;

            case QueryOperator.Ge:
                return Value >= query.Value;

            case QueryOperator.GeLe:
            case QueryOperator.GeLt:
            case QueryOperator.GtLe:
            case QueryOperator.GtLt:
                return Value >= query.Value && Value2 <= query.Value2;


            case QueryOperator.In:
                return _inValues.IsSubsetOf(query._inValues);

            default:
                return false;
        }
    }


    public override string ToString()
    {
        var result = InternalToString();
        return result;
    }

    private string InternalToString(bool withParamValues = true)
    {
        var result = PropertyName;
        switch (Operator)
        {
            case QueryOperator.Eq:
                result += " = ";
                break;
            case QueryOperator.Le:
                result += " <= ";
                break;
            case QueryOperator.Lt:
                result += " < ";
                break;
            case QueryOperator.Gt:
                result += " > ";
                break;
            case QueryOperator.Ge:
                result += " >= ";
                break;
            case QueryOperator.In:
                result += " In ";
                break;
            case QueryOperator.NotIn:
                result += " NOT In ";
                break;
            case QueryOperator.Contains:
                result += " Contains ";
                break;

            case QueryOperator.NotContains:
                result += " NOT Contains ";
                break;


            case QueryOperator.NotEq:
                result += " != ";
                break;

            case QueryOperator.StrStartsWith:
                result += " StartsWith ";
                break;

            case QueryOperator.StrEndsWith:
                result += " EndsWith ";
                break;

            case QueryOperator.StrContains:
                result += " Contains ";
                break;
        }


        if (Operator.IsRangeOperator())
        {
            var v1 = withParamValues ? Value.ToString() : "?";
            var v2 = withParamValues ? Value2.ToString() : "?";

            result += " in range ";

            if (Operator == QueryOperator.GeLe)
                result += $"[{v1}, {v2}]";
            else if (Operator == QueryOperator.GeLt)
                result += $"[{v1}, {v2})";
            else if (Operator == QueryOperator.GtLt)
                result += $"({v1}, {v2})";
            else if (Operator == QueryOperator.GtLe) result += $"({v1}, {v2}]";
        }
        else if (Operator is QueryOperator.In or QueryOperator.NotIn)
        {
            if (InValues.Count >= 4)
            {
                var values = withParamValues ? InValues.Count.ToString() : "?";
                result += "(" + values + " values)";
            }
            else
            {
                if (withParamValues)
                {
                    result += "(";
                    foreach (var keyValue in InValues)
                    {
                        result += keyValue;
                        result += " ";
                    }

                    result += ")";
                }
                else
                {
                    result += "(?)";
                }
            }
        }
        else
        {
            if (withParamValues)
            {
                result += Value.ToString();
                if (Value2 is not null)
                    result += ", " + Value2;
            }
            else
            {
                result += "?";
                if (Value2 is not null)
                    result += ", ?";
            }
        }

        return result;
    }

    /// <summary>
    ///     Check if an object matches the query
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public override bool Match(PackedObject item)
    {
        return Operator switch
        {
            QueryOperator.Eq => MatchEq(item, Value),
            QueryOperator.Ge => MatchGe(item, Value),
            QueryOperator.Gt => MatchGt(item, Value),
            QueryOperator.Le => MatchLe(item, Value),
            QueryOperator.Lt => MatchLt(item, Value),
            QueryOperator.GeLe => MatchGe(item, Value) && MatchLe(item, Value2),
            QueryOperator.GeLt => MatchGe(item, Value) && MatchLt(item, Value2),
            QueryOperator.GtLt => MatchGt(item, Value) && MatchLt(item, Value2),
            QueryOperator.GtLe => MatchGt(item, Value) && MatchLe(item, Value2),
            QueryOperator.In => MatchIn(item),
            QueryOperator.Contains => MatchContains(item),
            QueryOperator.NotContains => !MatchContains(item),
            QueryOperator.NotIn => !MatchIn(item),
            QueryOperator.NotEq => !MatchEq(item, Value),
            QueryOperator.StrStartsWith => MatchStartsWith(item, Value),
            QueryOperator.StrEndsWith => MatchEndsWith(item, Value),
            QueryOperator.StrContains => MatchContains(item, Value),
            _ => throw new ArgumentOutOfRangeException(nameof(item), "Unknown query operator"),
        };
    }

    private bool MatchLt(PackedObject item, KeyValue value)
    {
        return item[Metadata.Order] < value;
    }

    private bool MatchGt(PackedObject item, KeyValue value)
    {
        return item[Metadata.Order] > value;
    }

    private bool MatchLe(PackedObject item, KeyValue value)
    {
        return item[Metadata.Order] <= value;
    }

    private bool MatchGe(PackedObject item, KeyValue value)
    {
        return item[Metadata.Order] >= value;
    }

    private bool MatchIn(PackedObject item)
    {
        return GetValues().Contains(item[Metadata.Order]);
    }

    private bool MatchContains(PackedObject item)
    {
        var collection = item.Collection(Metadata.Order);

        return Array.Exists(collection.Values, v => Value == v);
    }


    private bool MatchContains(PackedObject item, KeyValue value)
    {
        var v1 = item[Metadata.Order].StringValue;
        var v2 = value.StringValue;

        return v1 != null && v2 != null && v1.Contains(v2, StringComparison.InvariantCultureIgnoreCase);
    }

    private bool MatchEq(PackedObject item, KeyValue value)
    {
        return value == item[Metadata.Order];
    }

    private bool MatchStartsWith(PackedObject item, KeyValue value)
    {
        var v1 = item[Metadata.Order].StringValue;
        var v2 = value.StringValue;

        return v1 != null && v2 != null && v1.StartsWith(v2, StringComparison.InvariantCultureIgnoreCase);
    }

    private bool MatchEndsWith(PackedObject item, KeyValue value)
    {
        var v1 = item[Metadata.Order].StringValue;
        var v2 = value.StringValue;

        return v1 != null && v2 != null && v1.EndsWith(v2, StringComparison.InvariantCultureIgnoreCase);
    }


    public AtomicQuery Clone()
    {
        if (InValues.Count > 0)
            return new(Metadata, InValues, Operator);

        if (Value2 is not null)
            return new(Metadata, Value, Value2);

        return new(Metadata, Value, Operator);
    }


    /// <summary>
    ///     Like <see cref="ToString" /> but without the parameter values
    ///     Describes a query category not a query instance
    /// </summary>
    /// <returns></returns>
    public string Description()
    {
        return InternalToString(false);
    }

    #region persistence

    /// <summary>
    ///     Primary value (the one used with unary operators)
    /// </summary>
    [field: ProtoMember(1)]
    public KeyValue Value { get; }

    /// <summary>
    ///     used for binary operators
    /// </summary>
    [field: ProtoMember(2)]
    private KeyValue Value2 { get; }

    /// <summary>
    ///     The operator of the atomic query
    /// </summary>
    [field: ProtoMember(3)]
    public QueryOperator Operator { get; }

    [ProtoMember(4)] private readonly HashSet<KeyValue> _inValues = new();

    [field: ProtoMember(5)] public KeyInfo Metadata { get; }

    #endregion
}