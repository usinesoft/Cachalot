#region

using System;
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
        private readonly CollectionSchema _collectionSchema;

        /// <summary>
        ///     Initialize for a specified cacheable data type
        /// </summary>
        /// <param name="type"></param>
        public QueryBuilder(Type type)
        {
            
            _collectionSchema = TypedSchemaFactory.FromType(type);
        }

        /// <summary>
        ///     Create from a <see cref="CollectionSchema" />
        /// </summary>
        /// <param name="collectionSchema"></param>
        public QueryBuilder(CollectionSchema collectionSchema)
        {
            _collectionSchema = collectionSchema ?? throw new ArgumentNullException(nameof(collectionSchema));
        }

        /// <summary>
        ///     Helper method. Convert a property value to <see cref="KeyValue" />
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="propertyValue"></param>
        /// <returns></returns>
        public KeyValue MakeKeyValue(string propertyName, object propertyValue)
        {
            var info = _collectionSchema.KeyByName(propertyName);
            return new KeyValue(propertyValue, info);
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
            var keyValue = MakeKeyValue(propertyName, value);
            var keyValue2 = MakeKeyValue(propertyName, value2);

            return new AtomicQuery(_collectionSchema.KeyByName(propertyName), keyValue, keyValue2);
        }

        
        /// <summary>
        ///     Get one by primary key
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public OrQuery GetOne(object value)
        {
            var keyValue = value as KeyValue ?? new KeyValue(value, _collectionSchema.PrimaryKeyField);

            var query = new OrQuery(_collectionSchema.CollectionName);
            var andQuery = new AndQuery();
            query.Elements.Add(andQuery);
            andQuery.Elements.Add(new AtomicQuery(_collectionSchema.PrimaryKeyField, keyValue));

            return query;
        }


        /// <summary>
        ///     Used to search by indexed scalar property (unique or not) (ordered indexes not supported)
        /// </summary>
        /// <param name="keyName"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public OrQuery In(string keyName, params object[] values)
        {
            var keyValues = values.Select(value => MakeKeyValue(keyName,value)).ToList();

            if (keyValues.Count == 0)
                throw new NotSupportedException("No index called " + keyName + " found");

            return new OrQuery(_collectionSchema.CollectionName)
            {
                Elements =
                {
                    new AndQuery
                    {
                        Elements =
                        {
                            new AtomicQuery(_collectionSchema.KeyByName(keyName), keyValues)
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
            var primary = _collectionSchema.PrimaryKeyField;
            var keyValues = values.Select(value => new KeyValue(value, primary)).ToList();


            return new OrQuery(_collectionSchema.CollectionName)
            {
                Elements =
                {
                    new AndQuery
                    {
                        Elements =
                        {
                            new AtomicQuery(primary, keyValues)
                        }
                    }
                }
            };
        }


        

        /// <summary>
        ///     Get many by multiple indexes
        /// </summary>
        /// <param name="binaryExpressions">a string formatted like "key operator value" </param>
        /// <returns></returns>
        public OrQuery GetMany(params string[] binaryExpressions)
        {
            var query = new OrQuery(_collectionSchema.CollectionName);

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

            if (_collectionSchema.PrimaryKeyField.Name.ToUpper() == left.ToUpper())
                keyInfo = _collectionSchema.PrimaryKeyField;

            if (keyInfo == null)
                foreach (var uniqueField in _collectionSchema.UniqueKeyFields)
                    if (uniqueField.Name.ToUpper() == left.ToUpper())
                        keyInfo = uniqueField;

            if (keyInfo == null)
                if (_collectionSchema.IndexFields != null)
                    foreach (var indexField in _collectionSchema.IndexFields)
                        if (indexField.Name.ToUpper() == left.ToUpper())
                            keyInfo = indexField;

            

            if (keyInfo == null)
                throw new ArgumentException(left + " is not an index field");


            KeyValue keyValue = null;
            
            // special processing for dates(must be in yyyy-mm-dd format)
            var parts = right.Split('-');
            if (parts.Length == 3)
            {
                var date = DateTime.Parse(right);

                keyValue = new KeyValue(date.Ticks, keyInfo);
            }
            else if (right.Contains(".")) // floating point value
            {
                if (decimal.TryParse(right, out var floatValue))
                    keyValue = new KeyValue(floatValue, keyInfo);
            }
            else // integer value
            {
                if (long.TryParse(right, out var longValue))
                    keyValue = new KeyValue(longValue, keyInfo);
            }

            keyValue ??= new KeyValue(right, keyInfo);
        

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

            var result = new AtomicQuery(keyInfo, keyValue, op);

            return result;
        }
    }
}