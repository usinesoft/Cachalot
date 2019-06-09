using System;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Client.Core;
using Client.Interface;
using Client.Messages;
using Client.Queries;
using Remotion.Linq;
using Remotion.Linq.Clauses;
using Remotion.Linq.Clauses.Expressions;
using Remotion.Linq.Clauses.ResultOperators;

namespace Cachalot.Linq
{
    public class QueryVisitor : QueryModelVisitorBase
    {
        private readonly TypeDescription _typeDescription;

        public QueryVisitor(TypeDescription typeDescription)
        {
            _typeDescription = typeDescription ?? throw new ArgumentNullException(nameof(typeDescription));
            RootExpression = new OrQuery(_typeDescription);
        }

        public OrQuery RootExpression { get; }


        public override void VisitQueryModel(QueryModel queryModel)
        {
            base.VisitQueryModel(queryModel);

            QueryHelper.OptimizeQuery(RootExpression);
        }

        private KeyValue AsKeyValue(MemberInfo member, object value)
        {
            var propertyDescription = _typeDescription.KeyByName(member.Name);

            var keyInfo = new KeyInfo(propertyDescription.KeyDataType, propertyDescription.KeyType,
                propertyDescription.Name, propertyDescription.IsOrdered);

            if (keyInfo.KeyType == KeyType.None)
                throw new NotSupportedException(
                    $"Property {member.Name} of type {member.DeclaringType?.Name} is not an index");

            return keyInfo.Value(value);
        }


        private bool IsLeafExpression(Expression expression)
        {
            return expression.NodeType == ExpressionType.GreaterThan
                   || expression.NodeType == ExpressionType.GreaterThanOrEqual
                   || expression.NodeType == ExpressionType.LessThan
                   || expression.NodeType == ExpressionType.LessThanOrEqual
                   || expression.NodeType == ExpressionType.Equal;
        }

        public override void VisitWhereClause(WhereClause whereClause, QueryModel queryModel, int index)
        {
            if (whereClause.Predicate is BinaryExpression expression)
            {
                VisitBinaryExpression(expression, RootExpression);
            }
            else
            {
                if (whereClause.Predicate is SubQueryExpression subQuery)
                {
                    AndQuery andExpression;

                    if (!RootExpression.MultipleWhereClauses)
                    {
                        andExpression = new AndQuery();
                        RootExpression.Elements.Add(andExpression);
                    }
                    else // multiple where clauses are joined by AND
                    {
                        andExpression = RootExpression.Elements[0];
                    }

                    var leaf = new AtomicQuery();
                    andExpression.Elements.Add(leaf);

                    VisitContainsExpression(subQuery, leaf);
                }
                else
                {
                    throw new NotSupportedException("Incorrect query");
                }
            }


            RootExpression.MultipleWhereClauses = true;

            base.VisitWhereClause(whereClause, queryModel, index);
        }


        public override void VisitResultOperator(ResultOperatorBase resultOperator, QueryModel queryModel, int index)
        {
            if (resultOperator is FirstResultOperator)
            {
                RootExpression.Take = 1;
                return;
            }

            if (resultOperator is CountResultOperator || resultOperator is LongCountResultOperator)
            {
                RootExpression.CountOnly = true;
                return;
            }

            if (resultOperator is TakeResultOperator takeResultOperator)
            {
                var exp = takeResultOperator.Count;

                if (exp.NodeType == ExpressionType.Constant)
                    RootExpression.Take = (int) ((ConstantExpression) exp).Value;
                else
                    throw new NotSupportedException(
                        "Currently not supporting methods or variables in the Skip or Take clause.");

                return;
            }

            if (resultOperator is SkipResultOperator @operator)
            {
                var exp = @operator.Count;

                if (exp.NodeType == ExpressionType.Constant)
                    RootExpression.Skip = (int) ((ConstantExpression) exp).Value;
                else
                    throw new NotSupportedException(
                        "Currently not supporting methods or variables in the Skip or Take clause.");

                return;
            }


            if (resultOperator is FullTextSearchResultOperator fullTextSearchResultOperator)
            {
                var param = fullTextSearchResultOperator.Parameter as ConstantExpression;

                RootExpression.FullTextSearch = (string) param.Value;
            }

            if (resultOperator is OnlyIfAvailableResultOperator) RootExpression.OnlyIfComplete = true;

            base.VisitResultOperator(resultOperator, queryModel, index);
        }

        private void VisitAndExpression(BinaryExpression binaryExpression, AndQuery andExpression)
        {
            if (IsLeafExpression(binaryExpression.Left))
            {
                andExpression.Elements.Add(VisitLeafExpression((BinaryExpression) binaryExpression.Left));
            }
            else if (binaryExpression.Left.NodeType == ExpressionType.AndAlso)
            {
                VisitAndExpression((BinaryExpression) binaryExpression.Left, andExpression);
            }
            else if (binaryExpression.Left.NodeType == ExpressionType.Extension)
            {
                if (binaryExpression.Left is SubQueryExpression subQuery)
                {
                    var leaf = new AtomicQuery();
                    andExpression.Elements.Add(leaf);
                    VisitContainsExpression(subQuery, leaf);
                }
            }
            else
            {
                throw new NotSupportedException("Query too complex");
            }

            if (IsLeafExpression(binaryExpression.Right))
            {
                andExpression.Elements.Add(VisitLeafExpression((BinaryExpression) binaryExpression.Right));
            }
            else if (binaryExpression.Right.NodeType == ExpressionType.Extension)
            {
                if (binaryExpression.Right is SubQueryExpression subQuery)
                {
                    var leaf = new AtomicQuery();
                    andExpression.Elements.Add(leaf);
                    VisitContainsExpression(subQuery, leaf);
                }
            }
            else
            {
                throw new NotSupportedException("Query too complex");
            }
        }

        private void VisitContainsExpression(SubQueryExpression subQuery, AtomicQuery leaf)
        {
            if (subQuery.QueryModel.ResultOperators.Count != 1)
                throw new NotSupportedException("Only Contains extension is supported");

            var contains = subQuery.QueryModel.ResultOperators[0] as ContainsResultOperator;

            // process collection.Contains(x=>x.Member)
            if (contains?.Item is MemberExpression item)
            {
                var select = subQuery.QueryModel?.SelectClause.Selector as QuerySourceReferenceExpression;

                if (select?.ReferencedQuerySource is MainFromClause from)
                {
                    var expression = from.FromExpression as ConstantExpression;

                    if (expression?.Value is IEnumerable values)
                    {
                        leaf.Operator = QueryOperator.In;

                        foreach (var value in values)
                        {
                            var kval = AsKeyValue(item.Member, value);
                            leaf.InValues.Add(kval);
                        }

                        return;
                    }
                }
            }
            // process x=>x.VectorMember.Contains(value)
            else
            {
                var value = contains?.Item;

                if (value != null)
                {
                    var select = subQuery.QueryModel?.SelectClause.Selector as QuerySourceReferenceExpression;
                    var from = select?.ReferencedQuerySource as MainFromClause;


                    if (from?.FromExpression is MemberExpression expression)
                    {
                        // the member must not be a scalar type. A string is a vector of chars but still considered a scalar in this context
                        var isVector = typeof(IEnumerable).IsAssignableFrom(expression.Type) &&
                                       !typeof(string).IsAssignableFrom(expression.Type);

                        if (!isVector)
                            throw new NotSupportedException("Trying to use Contains extension on a scalar member");


                        if (value is ConstantExpression valueExpession)
                        {
                            leaf.Operator = QueryOperator.In;

                            var kval = AsKeyValue(expression.Member, valueExpession.Value);

                            leaf.InValues.Add(kval);

                            return;
                        }
                    }
                }
            }

            throw new NotSupportedException("Only Contains extension is supported");
        }

        private void VisitBinaryExpression(BinaryExpression binaryExpression, OrQuery rootExpression)
        {
            // manage AND expressions
            if (binaryExpression.NodeType == ExpressionType.AndAlso)
            {
                var andExpression = new AndQuery();
                rootExpression.Elements.Add(andExpression);

                VisitAndExpression(binaryExpression, andExpression);
            }

            // manage OR expressions
            else if (binaryExpression.NodeType == ExpressionType.OrElse)
            {
                VisitOrExpression(binaryExpression, rootExpression);
            }

            // manage simple expressions like a > 10
            else if (IsLeafExpression(binaryExpression))
            {
                AndQuery andExpression;

                if (!rootExpression.MultipleWhereClauses)
                {
                    andExpression = new AndQuery();
                    rootExpression.Elements.Add(andExpression);
                }
                else // if multiple where clauses consider them as expressions linked by AND
                {
                    andExpression = rootExpression.Elements[0];
                }


                andExpression.Elements.Add(VisitLeafExpression(binaryExpression));
            }
            else
            {
                throw new NotSupportedException("Query too complex");
            }
        }

        //TODO add unit test for OR expression with Contains
        /// <summary>
        ///     OR expression can be present only at root level
        /// </summary>
        /// <param name="binaryExpression"></param>
        /// <param name="rootExpression"></param>
        private void VisitOrExpression(BinaryExpression binaryExpression, OrQuery rootExpression)
        {
            // visit left part
            if (IsLeafExpression(binaryExpression.Left))
            {
                var andExpression = new AndQuery();
                rootExpression.Elements.Add(andExpression);

                andExpression.Elements.Add(VisitLeafExpression((BinaryExpression) binaryExpression.Left));
            }
            else if (binaryExpression.Left.NodeType == ExpressionType.AndAlso)
            {
                var andExpression = new AndQuery();
                rootExpression.Elements.Add(andExpression);
                VisitAndExpression((BinaryExpression) binaryExpression.Left, andExpression);
            }
            else if (binaryExpression.Left.NodeType == ExpressionType.Extension)
            {
                if (binaryExpression.Left is SubQueryExpression subQuery)
                {
                    AndQuery andExpression;

                    if (!rootExpression.MultipleWhereClauses)
                    {
                        andExpression = new AndQuery();
                        rootExpression.Elements.Add(andExpression);
                    }
                    else // multiple where clauses are joined by AND
                    {
                        andExpression = rootExpression.Elements[0];
                    }


                    var leaf = new AtomicQuery();
                    andExpression.Elements.Add(leaf);

                    VisitContainsExpression(subQuery, leaf);
                }
            }
            else if (binaryExpression.Left.NodeType == ExpressionType.OrElse)
            {
                VisitOrExpression((BinaryExpression) binaryExpression.Left, rootExpression);
            }
            else if (binaryExpression.Left.NodeType == ExpressionType.AndAlso)
            {
                var andExpression = new AndQuery();
                rootExpression.Elements.Add(andExpression);
                VisitAndExpression((BinaryExpression) binaryExpression.Left, andExpression);
            }
            else
            {
                throw new NotSupportedException("Query too complex");
            }

            // visit right part
            if (IsLeafExpression(binaryExpression.Right))
            {
                var andExpression = new AndQuery();
                rootExpression.Elements.Add(andExpression);

                andExpression.Elements.Add(VisitLeafExpression((BinaryExpression) binaryExpression.Right));
            }
            else if (binaryExpression.Right.NodeType == ExpressionType.Extension)
            {
                if (binaryExpression.Right is SubQueryExpression subQuery)
                {
                    var andExpression = new AndQuery();
                    rootExpression.Elements.Add(andExpression);

                    if (rootExpression.MultipleWhereClauses)
                        throw new NotSupportedException(
                            "Multiple where clauses can be used only with simple expressions");


                    var leaf = new AtomicQuery();
                    andExpression.Elements.Add(leaf);
                    VisitContainsExpression(subQuery, leaf);
                }
            }
            else if (binaryExpression.Right.NodeType == ExpressionType.OrElse)
            {
                VisitOrExpression((BinaryExpression) binaryExpression.Right, rootExpression);
            }
            else if (binaryExpression.Right.NodeType == ExpressionType.AndAlso)
            {
                var andExpression = new AndQuery();
                rootExpression.Elements.Add(andExpression);
                VisitAndExpression((BinaryExpression) binaryExpression.Right, andExpression);
            }
            else
            {
                throw new NotSupportedException("Query too complex");
            }
        }

        // TODO add unit test for reverted expression : const = member

        /// <summary>
        ///     Manage simple expressions like left operator right
        /// </summary>
        /// <param name="binaryExpression"></param>
        private AtomicQuery VisitLeafExpression(BinaryExpression binaryExpression)
        {
            if (binaryExpression.Left is MemberExpression left && binaryExpression.Right is ConstantExpression right)
            {
                var kval = AsKeyValue(left.Member, right.Value);

                var oper = QueryOperator.Eq;


                if (binaryExpression.NodeType == ExpressionType.GreaterThan) oper = QueryOperator.Gt;

                if (binaryExpression.NodeType == ExpressionType.GreaterThanOrEqual) oper = QueryOperator.Ge;

                if (binaryExpression.NodeType == ExpressionType.LessThan) oper = QueryOperator.Lt;

                if (binaryExpression.NodeType == ExpressionType.LessThanOrEqual) oper = QueryOperator.Le;

                return new AtomicQuery(kval, oper);
            }

            // try to revert the expression
            left = binaryExpression.Right as MemberExpression;
            right = binaryExpression.Left as ConstantExpression;

            if (left != null && right != null)
            {
                var kval = AsKeyValue(left.Member, right.Value);

                var oper = QueryOperator.Eq;


                if (binaryExpression.NodeType == ExpressionType.GreaterThan) oper = QueryOperator.Le;

                if (binaryExpression.NodeType == ExpressionType.GreaterThanOrEqual) oper = QueryOperator.Lt;

                if (binaryExpression.NodeType == ExpressionType.LessThan) oper = QueryOperator.Ge;

                if (binaryExpression.NodeType == ExpressionType.LessThanOrEqual) oper = QueryOperator.Gt;

                return new AtomicQuery(kval, oper);
            }

            throw new NotSupportedException("Error parsing binary expression");
        }
    }
}