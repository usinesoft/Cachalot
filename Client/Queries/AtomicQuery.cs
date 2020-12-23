using System;
using System.Collections.Generic;
using System.Linq;
using Client.Core;
using ProtoBuf;

namespace Client.Queries
{
    /// <summary>
    ///     Just an operator and one or two values. 
    ///     This class is immutable.
    ///     The second value is useful only for Btw (between) operator
    /// </summary>
    [ProtoContract]
    public sealed class AtomicQuery : Query, IEquatable<AtomicQuery>
    {
        private HashSet<KeyValue> _inValues = new HashSet<KeyValue>();


        /// <summary>
        ///     Parameter-less constructor used for serialization
        /// </summary>
        public AtomicQuery()
        {
        }


        /// <summary>
        ///     Build a simple atomic query (one value and unary operator)
        /// </summary>
        /// <param name="value"></param>
        /// <param name="oper"></param>
        public AtomicQuery(KeyValue value, QueryOperator oper = QueryOperator.Eq)
        {
            Value = value;
            Operator = oper;
        }


        /// <summary>
        ///     Build an IN query
        /// </summary>
        /// <param name="values"></param>
        public AtomicQuery(IEnumerable<KeyValue> values)
        {
            _inValues = new HashSet<KeyValue>(values);
            Operator = QueryOperator.In;
        }

        /// <summary>
        ///     Build a query of the type BETWEEN value value2
        /// </summary>
        /// <param name="value"></param>
        /// <param name="value2"></param>
        public AtomicQuery(KeyValue value, KeyValue value2)
        {
            Value = value;
            Value2 = value2;

            Operator = QueryOperator.Btw; //the one and only binary operator
        }

        public IndexType
            IndexType => Values.First().KeyType;

        public string IndexName => !ReferenceEquals(Value, null) ? Value.KeyName : InValues.First().KeyName;


        /// <summary>
        ///     Check if the query is valid
        /// </summary>
        public override bool IsValid
        {
            get
            {
                // BETWEEN requires two values
                if (Operator == QueryOperator.Btw)
                {
                    if (ReferenceEquals(Value, null))
                        return false;

                    if (ReferenceEquals(Value2, null))
                        return false;

                    if (Value2 < Value)
                        return false;

                    // the two values must belong to the same index
                    if (Value.KeyName != Value2.KeyName)
                        return false;
                }


                // two values are valid only for BETWEEN operator
                if (!ReferenceEquals(Value2, null))
                    if (Operator != QueryOperator.Btw)
                        return false;

                // IN requires a list of values
                if (Operator == QueryOperator.In && InValues.Count == 0)
                    return false;

                // IN requires a list of values
                if (Operator == QueryOperator.Nin && InValues.Count == 0)
                    return false;

                // only IN accepts a list of values
                if (Operator != QueryOperator.In && Operator != QueryOperator.Nin && InValues.Count > 0)
                    return false;

                // any operator except IN requires at least a value
                if (Operator != QueryOperator.In && Operator != QueryOperator.Nin && ReferenceEquals(Value, null))
                    return false;

                // all values should belong to the same index key
                if (Operator == QueryOperator.In ||Operator == QueryOperator.Nin)
                {
                    var name = IndexName;
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
        public QueryOperator Operator { get; set; }

        [ProtoMember(4)]
        public ICollection<KeyValue> InValues
        {
            get => _inValues;
            set => _inValues = new HashSet<KeyValue>(value);
        }

        #endregion


        public IList<KeyValue> Values => _inValues.Count > 0
            ? _inValues.ToList()
            : !ReferenceEquals(Value2, null)
                ? new List<KeyValue> {Value, Value2}
                : new List<KeyValue> {Value};

        public bool Equals(AtomicQuery atomicQuery)
        {
            if (atomicQuery == null) return false;

            if (!Equals(Operator, atomicQuery.Operator)) return false;


            if (InValues.Count != atomicQuery.InValues.Count) return false;

            if (Operator == QueryOperator.In)
            {
                var myValues = _inValues.ToList();
                var rightValues = atomicQuery._inValues.ToList();

                for (var i = 0; i < myValues.Count; i++)
                    if (myValues[i] != rightValues[i])
                        return false;
            }


            if (ReferenceEquals(Value, null) && !ReferenceEquals(atomicQuery.Value, null))
                return false;

            if (!ReferenceEquals(Value, null) && ReferenceEquals(atomicQuery.Value, null))
                return false;

            if (ReferenceEquals(Value2, null) && !ReferenceEquals(atomicQuery.Value2, null))
                return false;
            if (!ReferenceEquals(Value2, null) && ReferenceEquals(atomicQuery.Value2, null))
                return false;

            if (Value != atomicQuery.Value)
                return false;

            if (Value2 != atomicQuery.Value2)
                return false;

            if (IndexName != atomicQuery.IndexName)
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
                if (!ReferenceEquals(Value, null) && !ReferenceEquals(Value2, null))
                    return Value.GetHashCode() + Value2.GetHashCode();

                if (!ReferenceEquals(Value2, null))
                    return Value2.GetHashCode();

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
        private static bool AreOperatorsCompatible(QueryOperator left, QueryOperator right)
        {
            if (left == right)
                return true;

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

            if (Value.KeyName != query.Value.KeyName)
                return false;

            Dbg.CheckThat(Value.Type == query.Value.Type);

            if (!AreOperatorsCompatible(Operator, rightOperator))
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

                default: //TODO check if In subdomains need to be implemented
                    return false;
            }
        }


        public override string ToString()
        {
            var result = IndexName;
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
                case QueryOperator.Nin:
                    result += " NOT In ";
                    break;
                case QueryOperator.Btw:
                    result += " Btw ";
                    break;
                
                case QueryOperator.Neq:
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


            if (Operator == QueryOperator.In ||Operator == QueryOperator.Nin )
            {
                if (InValues.Count >= 4)
                {
                    result += "(" + InValues.Count + " values)";
                }
                else
                {
                    result += "(";
                    foreach (var keyValue in InValues)
                    {
                        result += keyValue;
                        result += " ";
                    }

                    result += ")";
                }
            }
            else
            {
                result += Value.ToString();
                if (!ReferenceEquals(Value2, null))
                    result += ", " + Value2;
            }

            return result;
        }

        public bool IsComparison =>
            Operator == QueryOperator.Eq || Operator == QueryOperator.Le || Operator == QueryOperator.Lt || Operator == QueryOperator.Ge ||
            Operator == QueryOperator.Gt;

        /// <summary>
        ///     Check if an object matches the query
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public override bool Match(CachedObject item)
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
                    return MatchLs(item, Value);
                case QueryOperator.Btw:
                    return MatchGe(item, Value) && MatchLe(item, Value2);

                case QueryOperator.In:
                    return MatchIn(item);
            }

            throw new NotSupportedException("unknown operator");
        }

        private bool MatchLs(CachedObject item, KeyValue value)
        {
            return GetKeyOfObject(item, value) < value;
        }

        private bool MatchGt(CachedObject item, KeyValue value)
        {
            return GetKeyOfObject(item, value) > value;
        }

        private bool MatchLe(CachedObject item, KeyValue value)
        {
            return GetKeyOfObject(item, value) <= value;
        }

        private bool MatchGe(CachedObject item, KeyValue value)
        {
            return GetKeyOfObject(item, value) >= value;
        }

        private bool MatchIn(CachedObject item)
        {
            return item.MatchOneOf(_inValues);
        }

        private bool MatchEq(CachedObject item, KeyValue value)
        {
            return Value == GetKeyOfObject(item, value);
        }

        /// <summary>
        ///     Find the key of a cacheable object having the same Name, DataType and KeyType
        ///     as the specified <see cref="KeyValue" />
        /// </summary>
        /// <param name="item"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private KeyValue GetKeyOfObject(CachedObject item, KeyValue value)
        {
            if (value.KeyType == IndexType.Primary)
                return item.PrimaryKey;

            if (value.KeyType == IndexType.Unique)
            {
                foreach (var k in item.UniqueKeys)
                    if (k.KeyName == Value.KeyName)
                        return k;

                throw new NotSupportedException($"Can not match this item {item} with key {Value}");
            }

            if (value.KeyType == IndexType.Ordered || value.KeyType == IndexType.Dictionary)
            {
                foreach (var k in item.IndexKeys)
                    if (k.KeyName == Value.KeyName)
                        return k;

                throw new NotSupportedException($"Can not match this item {item} with key {Value}");
            }

            throw new NotSupportedException($"Can not match this item {item} with key {Value}");
        }


        public AtomicQuery Clone()
        {
            if (InValues.Count > 0)
                return new AtomicQuery(InValues);

            if (!ReferenceEquals(Value2, null))
                return new AtomicQuery(Value, Value2);

            return new AtomicQuery(Value, Operator);
        }
    }
}