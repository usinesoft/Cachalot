using System;
using System.Collections.Generic;
using System.Linq;
using Client.Core;
using Client.Interface;
using ProtoBuf;

namespace Client.Queries
{
    /// <summary>
    ///     Just an operator and one or two values. It can be solved by a single index
    ///     This class is immutable.
    ///     The second value is useful only for Btw (between) operator
    /// </summary>
    [ProtoContract]
    public sealed class AtomicQuery : Query, IEquatable<AtomicQuery>
    {
        [ProtoMember(4)] private readonly KeyValue _value;

        [ProtoMember(5)] private readonly KeyValue _value2;

        private HashSet<KeyValue> _inValues = new HashSet<KeyValue>();


        [ProtoMember(3)] private QueryOperator _operator;

        /// <summary>
        ///     Parameterless constructor used for serialization
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
            _value = value;
            _operator = oper;
        }


        /// <summary>
        ///     Build an IN query
        /// </summary>
        /// <param name="values"></param>
        public AtomicQuery(IEnumerable<KeyValue> values)
        {
            _inValues = new HashSet<KeyValue>(values);
            _operator = QueryOperator.In;
        }

        /// <summary>
        ///     Build a query of the type BETWEEN value value2
        /// </summary>
        /// <param name="value"></param>
        /// <param name="value2"></param>
        public AtomicQuery(KeyValue value, KeyValue value2)
        {
            _value = value;
            _value2 = value2;

            _operator = QueryOperator.Btw; //the one and only binary operator
        }

        public KeyType
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

                    // the two values must belog to the same index
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

                // only IN accepts a list of values
                if (Operator != QueryOperator.In && InValues.Count > 0)
                    return false;

                // any operator except IN requires at least a value
                if (Operator != QueryOperator.In && ReferenceEquals(Value, null))
                    return false;

                // all values should belong to the same index key
                if (Operator == QueryOperator.In)
                {
                    var name = IndexName;
                    if (InValues.Any(v => v.KeyName != name))
                        return false;
                }


                return true;
            }
        }

        /// <summary>
        ///     Primary value (the one used with unary operators)
        /// </summary>
        public KeyValue Value => _value;

        /// <summary>
        ///     used for binary operators
        /// </summary>
        public KeyValue Value2 => _value2;

        /// <summary>
        ///     The operator of the atomic query
        /// </summary>
        public QueryOperator Operator
        {
            get => _operator;
            set => _operator = value;
        }

        [ProtoMember(1)]
        public ICollection<KeyValue> InValues
        {
            get => _inValues;
            set => _inValues = new HashSet<KeyValue>(value);
        }

        public IList<KeyValue> Values => _inValues.Count > 0
            ? _inValues.ToList()
            : !ReferenceEquals(Value2, null)
                ? new List<KeyValue> {Value, Value2}
                : new List<KeyValue> {Value};

        public bool Equals(AtomicQuery atomicQuery)
        {
            if (atomicQuery == null) return false;

            if (!Equals(_operator, atomicQuery._operator)) return false;


            if (InValues.Count != atomicQuery.InValues.Count) return false;

            if (_operator == QueryOperator.In)
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
                return _operator.GetHashCode() + sum;
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
        private bool IsSubsetOf(AtomicQuery query)
        {
            var rightOperator = query.Operator;

            if (Value.KeyName != query.Value.KeyName)
                return false;

            Dbg.CheckThat(Value.KeyDataType == query.Value.KeyDataType);

            if (!AreOperatorsCompatible(_operator, rightOperator))
                return false;


            switch (_operator)
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

        /// <summary>
        ///     Check if the this query is a subset of a <see cref="DomainDescription" />
        ///     A domain describes the available data as a list of atomic queries considered as separated by an OR operator
        /// </summary>
        /// <param name="domain"></param>
        /// <returns></returns>
        public bool IsSubsetOf(DomainDescription domain)
        {
            if (domain.IsFullyLoaded)
                return true;

            return domain.GetCompleteQueriesByKey(Value.KeyName).Any(IsSubsetOf);
        }

        public override string ToString()
        {
            var result = IndexName;
            switch (_operator)
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
                case QueryOperator.Btw:
                    result += " Btw ";
                    break;
            }


            if (Operator == QueryOperator.In)
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
            if (value.KeyType == KeyType.Primary)
                return item.PrimaryKey;

            if (value.KeyType == KeyType.Unique)
            {
                foreach (var k in item.UniqueKeys)
                    if (k.KeyName == Value.KeyName)
                        return k;

                throw new NotSupportedException($"Can not match this item {item} with key {Value}");
            }

            if (value.KeyType == KeyType.ScalarIndex)
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