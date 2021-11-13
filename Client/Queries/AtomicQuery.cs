using System;
using System.Collections.Generic;
using System.Linq;
using Client.Core;
using Client.Messages;
using JetBrains.Annotations;
using ProtoBuf;

namespace Client.Queries
{
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
            if(oper == QueryOperator.In || oper == QueryOperator.NotIn || oper.IsRangeOperator())
                throw new ArgumentException("invalid operator");

            if(value.KeyName != metadata.Name)
                throw new ArgumentException("Mismatch between value and metadata");

            Metadata = metadata;
            Value = value;
            Operator = oper;
        }


        /// <summary>
        ///     Build an IN or NOT IN query
        /// </summary>
        /// <param name="metadata"></param>
        /// <param name="values"></param>
        /// <param name="oper">may be <see cref="QueryOperator.In"/> or <see cref="QueryOperator.NotIn"/></param>
        public AtomicQuery([NotNull] KeyInfo metadata, [NotNull] ICollection<KeyValue> values, QueryOperator oper = QueryOperator.In)
        {
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            if (values == null) throw new ArgumentNullException(nameof(values));

            if (metadata.IsCollection)
                throw new ArgumentException("An IN (or NOT IN) query applies to a scalar value");

            if(oper != QueryOperator.In && oper != QueryOperator.NotIn)
                throw new ArgumentException("invalid operator");

            if(values.Any(v=>v.KeyName != metadata.Name))
                throw new ArgumentException("Mismatch between value and metadata");

            Metadata = metadata;
            _inValues = new HashSet<KeyValue>(values);
            Operator = oper;
        }

        /// <summary>
        ///     Build a query of the type BETWEEN value value2
        /// </summary>
        /// <param name="metadata"></param>
        /// <param name="value"></param>
        /// <param name="value2"></param>
        /// <param name="oper">A range operator like <see cref="QueryOperator.GeLe"/></param>
        public AtomicQuery([NotNull] KeyInfo metadata, [NotNull] KeyValue value, [NotNull] KeyValue value2, QueryOperator oper = QueryOperator.GeLe)
        {

            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            Value = value ?? throw new ArgumentNullException(nameof(value));
            Value2 = value2 ?? throw new ArgumentNullException(nameof(value2));

            if(!oper.IsRangeOperator())
                throw new ArgumentException("Only range operators can be used with two values");

            if(value.KeyName != metadata.Name)
                throw new ArgumentException("Mismatch between value and metadata");

            if(value2.KeyName != metadata.Name)
                throw new ArgumentException("Mismatch between value and metadata");

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

                    // the two values must belong to the same collection
                    if (Value.KeyName != Value2.KeyName)
                        return false;
                }


                // two values are valid only for range operators
                if (!ReferenceEquals(Value2, null))
                    if (!Operator.IsRangeOperator())
                        return false;

                // IN requires a list of values
                if (Operator == QueryOperator.In && InValues.Count == 0)
                    return false;

                // NOT IN requires a list of values
                if (Operator == QueryOperator.NotIn && InValues.Count == 0)
                    return false;

                // only IN and NOT IN accept a list of values
                if (Operator != QueryOperator.In && Operator != QueryOperator.NotIn && InValues.Count > 0)
                    return false;

                // any operator except IN and NOT IN requires Value to be filled
                if (Operator != QueryOperator.In && Operator != QueryOperator.NotIn && ReferenceEquals(Value, null))
                    return false;

                // all values should belong to the same collection
                if (Operator == QueryOperator.In ||Operator == QueryOperator.NotIn)
                {
                    var name = PropertyName;
                    if (InValues.Any(v => v.KeyName != name))
                        return false;
                }


                return true;
            }
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
        public QueryOperator Operator { get;}

        [ProtoMember(4)] private readonly HashSet<KeyValue> _inValues = new HashSet<KeyValue>();

        [field: ProtoMember(5)] public KeyInfo Metadata { get; }
        
        #endregion

        public ICollection<KeyValue> InValues => _inValues;

        public IList<KeyValue> Values => _inValues.Count > 0
            ? _inValues.ToList()
            : !ReferenceEquals(Value2, null)
                ? new List<KeyValue> {Value, Value2}
                : new List<KeyValue> {Value};

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
                {
                    return Value.GetHashCode() + Operator.GetHashCode();
                }

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
                {
                    result += $"[{v1}, {v2}]";
                }
                else if (Operator == QueryOperator.GeLt)
                {
                    result += $"[{v1}, {v2})";
                }
                else if (Operator == QueryOperator.GtLt)
                {
                    result += $"({v1}, {v2})";
                }
                else if (Operator == QueryOperator.GtLe)
                {
                    result += $"({v1}, {v2}]";
                }
            }
            else if (Operator == QueryOperator.In || Operator == QueryOperator.NotIn)
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
                    if (!ReferenceEquals(Value2, null))
                        result += ", " + Value2;
                }
                else
                {
                    result += "?";
                    if (!ReferenceEquals(Value2, null))
                        result += ", ?";
                }
                
            }

            return result;
        }

        public bool IsComparison =>
            Operator == QueryOperator.Eq || Operator == QueryOperator.Le || Operator == QueryOperator.Lt || Operator == QueryOperator.Ge ||
            Operator == QueryOperator.Gt || Operator.IsRangeOperator();

        /// <summary>
        ///     Check if an object matches the query
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public override bool Match(PackedObject item)
        {
            switch (Operator)
            {

                case QueryOperator.Eq:
                    return MatchEq(item, Value);
                
                case QueryOperator.Ge:
                    return MatchGe(item, Value);
                
                case QueryOperator.Gt:
                    return MatchGt(item, Value);
                
                case QueryOperator.Le:
                    return MatchLe(item, Value);
                
                case QueryOperator.Lt:
                    return MatchLt(item, Value);
                
                case QueryOperator.GeLe:
                    return MatchGe(item, Value) && MatchLe(item, Value2);

                case QueryOperator.GeLt:
                    return MatchGe(item, Value) && MatchLt(item, Value2);

                case QueryOperator.GtLt:
                    return MatchGt(item, Value) && MatchLt(item, Value2);

                case QueryOperator.GtLe:
                    return MatchGt(item, Value) && MatchLe(item, Value2);

                case QueryOperator.In:
                    return MatchIn(item);

                case QueryOperator.Contains:
                    return MatchContains(item);

                case QueryOperator.NotContains:
                    return !MatchContains(item);

                case QueryOperator.NotIn:
                    return !MatchIn(item);
                
                case QueryOperator.NotEq:
                    return !MatchEq(item, Value);
                    
                case QueryOperator.StrStartsWith:
                    return MatchStartsWith(item, Value);

                case QueryOperator.StrEndsWith:
                    return MatchEndsWith(item, Value);

                case QueryOperator.StrContains:
                    return MatchContains(item, Value);

                default:
                    throw new ArgumentOutOfRangeException();
            }
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
            return Values.Contains(item[Metadata.Order]);
        }

        private bool MatchContains(PackedObject item)
        {
            var collection = item.Collection(Metadata.Order);

            return collection.Values.Any(v=>Value == v);
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

        private bool MatchContains(PackedObject item, KeyValue value)
        {
            var v1 = item[Metadata.Order].StringValue;
            var v2 = value.StringValue;

            return v1 != null && v2 != null && v1.Contains(v2, StringComparison.InvariantCultureIgnoreCase);

        }

        
        public AtomicQuery Clone()
        {
            if (InValues.Count > 0)
                return new AtomicQuery(Metadata, InValues, Operator);

            if (!ReferenceEquals(Value2, null))
                return new AtomicQuery(Metadata, Value, Value2);

            return new AtomicQuery(Metadata, Value, Operator);
        }


        /// <summary>
        /// Like <see cref="ToString"/> but without the parameter values
        /// Describes a query category not a query instance
        /// </summary>
        /// <returns></returns>
        public string Description()
        {
            return InternalToString(false);
        }
    }
}