using Client.Core;
using Client.Queries;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Client.Parsing
{
    public static class SqlExtensions
    {
        public static OrQuery ToQuery(this Node node, CollectionSchema schema)
        {

            OrQuery result = null;

            if (node.Token is "select" or "count")
            {
                result = SelectToQuery(node, schema);
            }

            QueryHelper.OptimizeQuery(result);

            if (node.Token == "count")
            {
                result!.CountOnly = true;
            }

            return result;
        }

        private static OrQuery SelectToQuery(this Node node, CollectionSchema schema)
        {

            var tableNode = node.Children.FirstOrDefault(n => n.Token == "from");
            if (tableNode == null)
            {
                throw new NotSupportedException("Collection name not specified");
            }



            var query = new OrQuery(tableNode.Children.First().Token); // the one qnd only child of the "from" node is the table name

            // projection
            var projection = node.Children.FirstOrDefault(n => n.Token == "projection");

            if (projection != null)
                foreach (var column in projection.Children)
                {
                    var name = column.Token;

                    if (name != "*")
                    {
                        if (name == "distinct")
                        {
                            query.Distinct = true;
                        }
                        else
                        {
                            if (schema.KeyByName(name) == null)
                            {
                                throw new NotSupportedException($"{name} is not a server-side value");
                            }

                            var alias = column.Children.FirstOrDefault()?.Token ?? name;
                            query.SelectClause.Add(new SelectItem { Name = name, Alias = alias });
                        }

                    }

                }


            // where
            var where = node.Children.FirstOrDefault(c => c.Token == "where");

            if (where != null)
            {
                ParseWhere(where, query, schema);
            }


            // order by
            var order = node.Children.FirstOrDefault(c => c.Token == "order");

            if (order != null)
            {
                if (order.ErrorMessage != null)
                    throw new NotSupportedException(order.ErrorMessage);

                query.OrderByProperty = order.Children.FirstOrDefault()?.Token;
                if (order.Children.Count == 2)
                {
                    query.OrderByIsDescending = true;
                }
            }

            // take
            var take = node.Children.FirstOrDefault(c => c.Token == "take");

            if (take != null)
            {
                int count = int.Parse(take.Children.FirstOrDefault()?.Token ?? string.Empty);

                query.Take = count;
            }

            return query;
        }

        private static void ParseWhere(Node where, OrQuery query, CollectionSchema schema)
        {
            var orNodes = where.Children.Where(c => c.Token == "or").ToList();

            if (orNodes.Count != 1)
            {
                throw new NotSupportedException("Query too complex");
            }

            var orNode = orNodes.Single();

            var andNodes = orNode.Children.Where(c => c.Token == "and").ToList();

            foreach (var andNode in andNodes)
            {
                var andQuery = new AndQuery();

                foreach (var node in andNode.Children)
                {
                    var operands = node.Children.Select(c => c.Token).ToList();


                    var op = ParseOperator(node.Token, operands.LastOrDefault());


                    if (op is QueryOperator.In or QueryOperator.NotIn)
                    {
                        var metadata = schema.KeyByName(operands[0]);
                        if (metadata != null)
                        {
                            List<KeyValue> values = new List<KeyValue>();

                            foreach (var val in operands.Skip(1))
                            {
                                object value = JExtensions.SmartParse(val);
                                values.Add(new KeyValue(value));
                            }


                            andQuery.Elements.Add(new AtomicQuery(metadata, values, op));
                        }
                        else
                        {
                            throw new NotSupportedException($"Can not parse query after IN operator. {operands[0]} is a server-side value");

                        }
                    }

                    else if (op is QueryOperator.Contains or QueryOperator.NotContains) // for contains operator the collection property should be at the left side
                    {
                        var metadata = schema.KeyByName(operands[0]);

                        object value = JExtensions.SmartParse(operands[1]);

                        andQuery.Elements.Add(new AtomicQuery(metadata, new KeyValue(value), op));

                    }
                    else if (op is QueryOperator.StrStartsWith or QueryOperator.StrEndsWith or QueryOperator.StrContains) // for string operators the property should be at the left side
                    {
                        var metadata = schema.KeyByName(operands[0]);

                        object value = operands[1].Trim('\'', '"').Trim('%');

                        andQuery.Elements.Add(new AtomicQuery(metadata, new KeyValue(value), op));

                    }
                    else if (operands.Count == 2)// binary operators
                    {

                        var metadata = schema.KeyByName(operands[0]);
                        // by default property name first
                        if (metadata != null)
                        {
                            object value = JExtensions.SmartParse(operands[1]);

                            andQuery.Elements.Add(new AtomicQuery(metadata, new KeyValue(value), op));
                        }
                        else // try value first
                        {
                            metadata = schema.KeyByName(operands[1]);

                            if (metadata == null)
                            {
                                throw new NotSupportedException($"Can not parse query. Neither {operands[0]} nor {operands[1]} is a server-side value");
                            }


                            var value = JExtensions.SmartParse(operands[0]); 

                            andQuery.Elements.Add(new AtomicQuery(metadata, new KeyValue(value), Reverse(op)));

                        }

                    }
                }


                query.Elements.Add(andQuery);
            }

            if (query.Elements.Count == 1 && query.Elements[0].Elements.Count == 1)
            {
                var atomic = query.Elements[0].Elements[0];

                if (atomic.Operator == QueryOperator.Eq && atomic.Metadata.Name == schema.PrimaryKeyField.Name)
                {
                    query.ByPrimaryKey = true;
                }

            }



        }

        private static QueryOperator ParseOperator(string op, string valueAfter = null)
        {
            switch (op)
            {
                case "=":
                    return QueryOperator.Eq;
                case "!=":
                    return QueryOperator.NotEq;
                case "<=":
                    return QueryOperator.Le;
                case "<":
                    return QueryOperator.Lt;
                case ">=":
                    return QueryOperator.Ge;
                case ">":
                    return QueryOperator.Gt;
                case "in":
                    return QueryOperator.In;
                case "not in":
                    return QueryOperator.NotIn;
                case "contains":
                    return QueryOperator.Contains;
                case "not contains":
                    return QueryOperator.NotContains;
                case "like":
                    if (valueAfter != null)
                    {
                        valueAfter = valueAfter.Trim('\'', '"');
                        if (valueAfter.StartsWith("%") && valueAfter.EndsWith("%"))
                        {
                            return QueryOperator.StrContains;
                        }

                        if (valueAfter.StartsWith("%"))
                        {
                            return QueryOperator.StrEndsWith;
                        }

                        if (valueAfter.EndsWith("%"))
                        {
                            return QueryOperator.StrStartsWith;
                        }
                    }

                    break;


            }

            throw new NotSupportedException($"Unknown operator:{op}");
        }

        private static QueryOperator Reverse(QueryOperator op)
        {
            var result = op;

            switch (op)
            {
                case QueryOperator.Eq:
                    break;
                case QueryOperator.Gt:
                    result = QueryOperator.Lt;
                    break;
                case QueryOperator.Ge:
                    result = QueryOperator.Le;
                    break;
                case QueryOperator.Lt:
                    result = QueryOperator.Gt;
                    break;
                case QueryOperator.Le:
                    result = QueryOperator.Ge;
                    break;
                case QueryOperator.GeLe:
                    break;
                case QueryOperator.GtLe:
                    break;
                case QueryOperator.GtLt:
                    break;
                case QueryOperator.GeLt:
                    break;
                case QueryOperator.In:
                    break;
                case QueryOperator.Contains:
                    break;
                case QueryOperator.NotEq:
                    break;
                case QueryOperator.NotIn:
                    break;
                case QueryOperator.NotContains:
                    break;
                case QueryOperator.StrStartsWith:
                    break;
                case QueryOperator.StrEndsWith:
                    break;
                case QueryOperator.StrContains:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(op), op, null);
            }

            return result;
        }



    }
}