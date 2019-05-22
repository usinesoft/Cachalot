#region

using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Core;
using Client.Interface;
using Client.Messages;

#endregion

namespace Client.Queries
{
    /// <summary>
    ///     Helper class (produce queries for the most common cases)
    /// </summary>
    public class QueryBuilder
    {
        private readonly TypeDescription _typeDescription;

        /// <summary>
        ///     Initialize for a specified cacheable data type
        /// </summary>
        /// <param name="type"></param>
        public QueryBuilder(Type type)
        {
            var clientTypeDescription = ClientSideTypeDescription.RegisterType(type);

            _typeDescription = clientTypeDescription.AsTypeDescription;
        }

        /// <summary>
        ///     Create from a <see cref="TypeDescription" />
        /// </summary>
        /// <param name="typeDescription"></param>
        public QueryBuilder(TypeDescription typeDescription)
        {
            _typeDescription = typeDescription ?? throw new ArgumentNullException(nameof(typeDescription));
        }

        /// <summary>
        ///     Helper method. Convert a property value to <see cref="KeyValue" />
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="propertyValue"></param>
        /// <returns></returns>
        public KeyValue MakeKeyValue(string propertyName, object propertyValue)
        {
            return _typeDescription.MakeKeyValue(propertyName, propertyValue);
        }

        /// <summary>
        ///     Create an atomic query (propertyName oper value)
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="oper"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public AtomicQuery MakeAtomicQuery(string propertyName, QueryOperator oper, object value)
        {
            var keyValue = _typeDescription.MakeKeyValue(propertyName, value);

            return new AtomicQuery(keyValue, oper);
        }

        /// <summary>
        ///     Create an atomic "between" query
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="value"></param>
        /// <param name="value2"></param>
        /// <returns></returns>
        public AtomicQuery MakeAtomicQuery(string propertyName, object value, object value2)
        {
            var keyValue = _typeDescription.MakeKeyValue(propertyName, value);
            var keyValue2 = _typeDescription.MakeKeyValue(propertyName, value2);

            return new AtomicQuery(keyValue, keyValue2);
        }

        /// <summary>
        ///     Create an atomic query (propertyName = value)
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public AtomicQuery MakeAtomicQuery(string propertyName, object value)
        {
            var keyValue = _typeDescription.MakeKeyValue(propertyName, value);

            return new AtomicQuery(keyValue);
        }


        /// <summary>
        ///     Make an IN query
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public AtomicQuery MakeAtomicInQuery(string propertyName, IEnumerable values)
        {
            return new AtomicQuery(values.Cast<object>().Select(v => _typeDescription.MakeKeyValue(propertyName, v)));
        }


        public AndQuery MakeAndQuery()
        {
            return new AndQuery();
        }

        public AndQuery MakeAndQuery(AtomicQuery atomicQuery)
        {
            var q = new AndQuery();
            q.Elements.Add(atomicQuery);
            return q;
        }

        public OrQuery MakeOrQuery()
        {
            return new OrQuery(_typeDescription.FullTypeName);
        }

        public OrQuery MakeOrQuery(AndQuery andQuery)
        {
            var q = new OrQuery(_typeDescription.FullTypeName);
            q.Elements.Add(andQuery);

            return q;
        }

        public OrQuery MakeOrQuery(AtomicQuery atomicQuery)
        {
            var orQuery = new OrQuery(_typeDescription.FullTypeName);
            var andQuery = new AndQuery();
            andQuery.Elements.Add(atomicQuery);
            orQuery.Elements.Add(andQuery);
            return orQuery;
        }

        /// <summary>
        ///     Get one by unique key
        /// </summary>
        /// <param name="keyName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public OrQuery GetOne(string keyName, object value)
        {
            var keyValue = _typeDescription.MakeUniqueKeyValue(keyName, value);

            var query = new OrQuery(_typeDescription.FullTypeName);
            var andQuery = new AndQuery();
            query.Elements.Add(andQuery);
            andQuery.Elements.Add(new AtomicQuery(keyValue));

            return query;
        }


        /// <summary>
        ///     Get one by primary key
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public OrQuery GetOne(object value)
        {
            var keyValue = _typeDescription.MakePrimaryKeyValue(value);

            var query = new OrQuery(_typeDescription.FullTypeName);
            var andQuery = new AndQuery();
            query.Elements.Add(andQuery);
            andQuery.Elements.Add(new AtomicQuery(keyValue));

            return query;
        }

        public KeyValue MakePrimaryKeyValue(object value)
        {
            return _typeDescription.MakePrimaryKeyValue(value);
        }


        ///// <summary>
        /////     Create a query of the type:  where key In (value1, value2, value3)
        /////     Works for every type of key
        ///// </summary>
        ///// <param name="keyName"></param>
        ///// <param name="values"></param>
        ///// <returns></returns>
        //public OrQuery In(string keyName, params object[] values)
        //{
        //    var query = new OrQuery(_typeDescription.FullTypeName);

        //    foreach (var value in values)
        //    {
        //        var andQuery = new AndQuery();
        //        query.Elements.Add(andQuery);
        //        var keyValue = _typeDescription.MakeKeyValue(keyName, value);
        //        andQuery.Elements.Add(new AtomicQuery(keyValue));
        //    }

        //    return query;
        //}

        ///// <summary>
        /////     Create a query of the type:  where primary_key In (value1, value2, value3)
        ///// </summary>
        ///// <param name="values"></param>
        ///// <returns></returns>
        //public OrQuery In(params object[] values)
        //{
        //    var query = new OrQuery(_typeDescription.FullTypeName);

        //    foreach (var value in values)
        //    {
        //        var andQuery = new AndQuery();
        //        query.Elements.Add(andQuery);
        //        var keyValue = _typeDescription.MakePrimaryKeyValue(value);
        //        andQuery.Elements.Add(new AtomicQuery(keyValue));
        //    }

        //    return query;
        //}


        /// <summary>
        ///     Used to search by indexed list property. Find all the objects
        /// </summary>
        /// <param name="keyName"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public OrQuery Contains(string keyName, params object[] values)
        {
            var keyValues = _typeDescription.MakeIndexedListKeyValues(keyName, values).ToList();

            if (keyValues.Count == 0)
                throw new NotSupportedException("No list index called " + keyName + " found");

            return new OrQuery(_typeDescription.FullTypeName)
            {
                Elements =
                {
                    new AndQuery
                    {
                        Elements =
                        {
                            new AtomicQuery(keyValues)
                        }
                    }
                }
            };
        }


        /// <summary>
        ///     Used to search by indexed scalar property (unique or not) (ordered indexes not supported)
        /// </summary>
        /// <param name="keyName"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public OrQuery In(string keyName, params object[] values)
        {
            var keyValues = values.Select(value => _typeDescription.MakeKeyValue(keyName, value)).ToList();

            if (keyValues.Count == 0)
                throw new NotSupportedException("No index called " + keyName + " found");

            return new OrQuery(_typeDescription.FullTypeName)
            {
                Elements =
                {
                    new AndQuery
                    {
                        Elements =
                        {
                            new AtomicQuery(keyValues)
                        }
                    }
                }
            };
        }

        /// <summary>
        ///     Search by primary key in a list of values
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public OrQuery In(params object[] values)
        {
            var keyValues = values.Select(value => _typeDescription.MakePrimaryKeyValue(value)).ToList();


            return new OrQuery(_typeDescription.FullTypeName)
            {
                Elements =
                {
                    new AndQuery
                    {
                        Elements =
                        {
                            new AtomicQuery(keyValues)
                        }
                    }
                }
            };
        }


        /// <summary>
        ///     Crate o query of the type "where IndexKey oper value"
        /// </summary>
        /// <param name="keyName"></param>
        /// <param name="value"></param>
        /// <param name="oper"></param>
        /// <returns></returns>
        public OrQuery GetMany(string keyName, object value, QueryOperator oper = QueryOperator.Eq)
        {
            var keyValue = _typeDescription.MakeIndexKeyValue(keyName, value);

            var query = new OrQuery(_typeDescription.FullTypeName);
            var andQuery = new AndQuery();
            query.Elements.Add(andQuery);
            andQuery.Elements.Add(new AtomicQuery(keyValue, oper));

            return query;
        }


        /// <summary>
        ///     Get many by multiple indexes
        /// </summary>
        /// <param name="binaryExpressions">a string formatted like "key operator value" </param>
        /// <returns></returns>
        public OrQuery GetMany(params string[] binaryExpressions)
        {
            var query = new OrQuery(_typeDescription.FullTypeName);

            if (binaryExpressions.Length > 0)
            {
                var andQuery = new AndQuery();
                query.Elements.Add(andQuery);

                foreach (var expression in binaryExpressions)
                {
                    var q = StringToQuery(expression);
                    andQuery.Elements.Add(q);
                }


                QueryHelper.OptimizeQuery(query);
            }
            

            return query;
        }

        /// <summary>
        ///     Create an <see cref="OrQuery" /> from a string similar to SQL WHERE clauses
        /// </summary>
        /// <param name="sqlLike"></param>
        /// <returns></returns>
        public OrQuery GetManyWhere(string sqlLike)
        {
            var binaryExpressions = sqlLike.Split(',');
            for (var i = 0; i < binaryExpressions.Length; i++)
                binaryExpressions[i] = binaryExpressions[i].Trim();
            return GetMany(binaryExpressions);
        }

        /// <summary>
        ///     Parse a string like "key == value" or "key &gt; value"
        /// </summary>
        /// <param name="queryString"></param>
        /// <returns></returns>
        private AtomicQuery StringToQuery(string queryString)
        {
            var expression = new Regex("(\\w+)\\s*(==|=|<=|<|>|>=|CONTAINS)\\s*((\\w|-|\\.)+)");
            var match = expression.Match(queryString);
            if (!match.Success || match.Captures.Count != 1 || match.Groups.Count != 5)
                throw new ArgumentException($"Invalid query string {queryString}");


            var left = match.Groups[1].Value;
            left = left.Trim();
            var oper = match.Groups[2].Value;
            oper = oper.Trim();
            var right = match.Groups[3].Value;
            right = right.Trim();

            KeyInfo keyInfo = null;

            if (_typeDescription.PrimaryKeyField.Name.ToUpper() == left.ToUpper())
                keyInfo = _typeDescription.PrimaryKeyField;

            if (keyInfo == null)
                foreach (var uniqueField in _typeDescription.UniqueKeyFields)
                    if (uniqueField.Name.ToUpper() == left.ToUpper())
                        keyInfo = uniqueField;

            if (keyInfo == null)
                if (_typeDescription.IndexFields != null)
                    foreach (var indexField in _typeDescription.IndexFields)
                        if (indexField.Name.ToUpper() == left.ToUpper())
                            keyInfo = indexField;

            if (keyInfo == null)
                foreach (var indexField in _typeDescription.ListFields)
                    if (indexField.Name.ToUpper() == left.ToUpper())
                        keyInfo = indexField;

            if (keyInfo == null)
                throw new ArgumentException(left + " is not an index field");


            KeyValue keyValue;
            if (keyInfo.KeyDataType == KeyDataType.IntKey)
            {
                // special processing for dates(must be in yyyy-mm-dd format)
                var parts = right.Split('-');
                if (parts.Length == 3)
                {
                    var date = DateTime.Parse(right);

                    keyValue = new KeyValue(date.Ticks, keyInfo);
                }
                else
                {
                    if (right.Contains(".")) // floating point value
                    {
                        if (!decimal.TryParse(right, out var floatValue))
                            throw new ArgumentException(right + " can not be converted to float");

                        keyValue = KeyInfo.ValueToKeyValue(floatValue, keyInfo);
                    }
                    else // integer value
                    {
                        if (!long.TryParse(right, out var longValue))
                            throw new ArgumentException(right + " can not be converted to long");


                        keyValue = KeyInfo.ValueToKeyValue(longValue, keyInfo);
                    }
                }
            }
            else
            {
                keyValue = new KeyValue(right, keyInfo);
            }


            QueryOperator op;
            switch (oper.ToLower())
            {
                case "=":
                case "==":
                    op = QueryOperator.Eq;
                    break;
                case "<":
                    op = QueryOperator.Lt;
                    break;
                case "<=":
                    op = QueryOperator.Le;
                    break;
                case ">":
                    op = QueryOperator.Gt;
                    break;
                case ">=":
                    op = QueryOperator.Ge;
                    break;
                case "CONTAINS":
                    op = QueryOperator.In;
                    break;
                default:
                    throw new ArgumentException("unknown operator: ", oper);
            }

            var result = new AtomicQuery(keyValue, op);

            return result;
        }
    }
}