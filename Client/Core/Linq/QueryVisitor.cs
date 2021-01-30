using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Client.Interface;
using Client.Messages;
using Client.Queries;
using JetBrains.Annotations;
using Remotion.Linq;
using Remotion.Linq.Clauses;
using Remotion.Linq.Clauses.Expressions;
using Remotion.Linq.Clauses.ResultOperators;

namespace Client.Core.Linq
{
    public class QueryVisitor : QueryModelVisitorBase
    {
        private readonly CollectionSchema _collectionSchema;

        public QueryVisitor([NotNull] string collectionName, [NotNull] CollectionSchema collectionSchema)
        {
            if (string.IsNullOrEmpty(collectionName))
                throw new ArgumentException("Value cannot be null or empty.", nameof(collectionName));

            _collectionSchema = collectionSchema ?? throw new ArgumentNullException(nameof(collectionSchema));

            RootExpression = new OrQuery(collectionName);
        }

        public OrQuery RootExpression { get; }


        public override void VisitQueryModel(QueryModel queryModel)
        {
            base.VisitQueryModel(queryModel);

            QueryHelper.OptimizeQuery(RootExpression);
        }


        private KeyInfo GetMetadata(MemberInfo member)
        {
            var propertyDescription = _collectionSchema.KeyByName(member.Name);
            if (propertyDescription == null)
                throw new CacheException($"property {member.Name} is not servers-side visible");

            return propertyDescription;
        }


        private bool IsLeafExpression(Expression expression)
        {
            return expression.NodeType == ExpressionType.GreaterThan
                   || expression.NodeType == ExpressionType.GreaterThanOrEqual
                   || expression.NodeType == ExpressionType.LessThan
                   || expression.NodeType == ExpressionType.LessThanOrEqual
                   || expression.NodeType == ExpressionType.Equal
                   || expression.NodeType == ExpressionType.NotEqual
                ;
        }


        public override void VisitWhereClause(WhereClause whereClause, QueryModel queryModel, int index)
        {
            if (whereClause.Predicate.NodeType == ExpressionType.Not)
            {
                var unary = (UnaryExpression) whereClause.Predicate;
                InternalVisitWhereClause(unary.Operand, true);
            }
            else
            {
                InternalVisitWhereClause(whereClause.Predicate);
            }
        }

        private void InternalVisitWhereClause(Expression whereClause, bool not = false)
        {
            if (whereClause is BinaryExpression expression)
            {
                VisitBinaryExpression(expression, RootExpression);
            }
            else if (whereClause is MemberExpression memberExpression)
            {
                if (memberExpression.Type == typeof(bool))
                    // Generalize for more complex expressions
                    VisitMemberExpression(memberExpression, RootExpression, not);
            }
            else if (whereClause is MethodCallExpression call)
            {
                VisitMethodCall(call, RootExpression);
            }
            else
            {
                if (whereClause is SubQueryExpression subQuery)
                {
                    var atomicQuery = VisitContainsExpression(subQuery, not);
                    var andQuery = new AndQuery();
                    andQuery.Elements.Add(atomicQuery);
                    RootExpression.Elements.Add(andQuery);
                }
                else
                {
                    throw new NotSupportedException("Incorrect query");
                }
            }


            RootExpression.MultipleWhereClauses = true;
        }

        public override void VisitSelectClause(SelectClause selectClause, QueryModel queryModel)
        {
            var expression = selectClause.Selector;

            if (expression is MemberExpression member)
            {
                RootExpression.SelectClause.Add(new SelectItem{Name = member.Member.Name, Alias = member.Member.Name});
            }
                
            else if (expression is NewExpression @new)
            {
                for (int i = 0; i < @new.Arguments.Count; i++)
                {
                    var arg = @new.Arguments[i];

                    var targetMember = @new.Members[i];

                    if (arg is MemberExpression argMemberExpression)
                    {
                        RootExpression.SelectClause.Add(new SelectItem{Name = argMemberExpression.Member.Name, Alias = targetMember.Name});
                    }

                }
                
                    
            }
                

            base.VisitSelectClause(selectClause, queryModel);
        }

        public override void VisitOrderByClause(OrderByClause orderByClause, QueryModel queryModel, int index)
        {
            if (orderByClause.Orderings.Count > 1)
                throw new NotSupportedException("Only one order by clause is supported in this version");

            if (orderByClause.Orderings.Count == 1)
            {
                var ordering = orderByClause.Orderings[0];

                if (ordering.Expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                {
                    if (unary.Operand is MemberExpression member)
                    {
                        RootExpression.OrderByProperty = member.Member.Name;
                        RootExpression.OrderByIsDescending = ordering.OrderingDirection == OrderingDirection.Desc;
                    }
                }
                else if (ordering.Expression is MemberExpression member)
                {
                    RootExpression.OrderByProperty = member.Member.Name;
                    RootExpression.OrderByIsDescending = ordering.OrderingDirection == OrderingDirection.Desc;
                }
                else
                {
                    throw new NotSupportedException("Invalid order by clause");
                }
            }
        }

        public override void VisitResultOperator(ResultOperatorBase resultOperator, QueryModel queryModel, int index)
        {
            if (resultOperator is DistinctResultOperator)
            {
                RootExpression.Distinct = true;
                return;
            }

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
                if (fullTextSearchResultOperator.Parameter is ConstantExpression param)
                    RootExpression.FullTextSearch = (string) param.Value;

            if (resultOperator is OnlyIfAvailableResultOperator) RootExpression.OnlyIfComplete = true;

            base.VisitResultOperator(resultOperator, queryModel, index);
        }

        private void VisitAndExpression(BinaryExpression binaryExpression, AndQuery andExpression)
        {
            if (IsLeafExpression(binaryExpression.Left))
            {
                andExpression.Elements.Add(VisitLeafExpression((BinaryExpression) binaryExpression.Left));
            }
            else if (binaryExpression.Left.NodeType == ExpressionType.MemberAccess)
            {
                VisitMemberExpression((MemberExpression) binaryExpression.Left, andExpression, false);
            }
            else if (binaryExpression.Left.NodeType == ExpressionType.AndAlso)
            {
                VisitAndExpression((BinaryExpression) binaryExpression.Left, andExpression);
            }
            else if (binaryExpression.Left.NodeType == ExpressionType.Extension)
            {
                if (binaryExpression.Left is SubQueryExpression subQuery)
                {
                    var atomicQuery = VisitContainsExpression(subQuery);
                    andExpression.Elements.Add(atomicQuery);
                }
            }
            else if (binaryExpression.Left.NodeType == ExpressionType.Not)
            {
                var unary = (UnaryExpression) binaryExpression.Left;

                if (unary.Operand is SubQueryExpression subQuery)
                {
                    var atomicQuery = VisitContainsExpression(subQuery, true);
                    andExpression.Elements.Add(atomicQuery);
                }
                else if (unary.Operand is MemberExpression member)
                {
                    VisitMemberExpression(member, andExpression, true);
                }
            }
            else if (binaryExpression.Left is MethodCallExpression call)
            {
                VisitMethodCall(call, andExpression);
            }
            else
            {
                throw new NotSupportedException("Query too complex");
            }

            if (IsLeafExpression(binaryExpression.Right))
            {
                andExpression.Elements.Add(VisitLeafExpression((BinaryExpression) binaryExpression.Right));
            }
            else if (binaryExpression.Right.NodeType == ExpressionType.MemberAccess)
            {
                VisitMemberExpression((MemberExpression) binaryExpression.Right, andExpression, false);
            }
            else if (binaryExpression.Right.NodeType == ExpressionType.Extension)
            {
                if (binaryExpression.Right is SubQueryExpression subQuery)
                {
                    var atomicQuery = VisitContainsExpression(subQuery);
                    andExpression.Elements.Add(atomicQuery);
                }
            }
            else if (binaryExpression.Right.NodeType == ExpressionType.Not)
            {
                var unary = (UnaryExpression) binaryExpression.Right;

                if (unary.Operand is SubQueryExpression subQuery)
                {
                    var atomicQuery = VisitContainsExpression(subQuery, true);
                    andExpression.Elements.Add(atomicQuery);
                }
                else if (unary.Operand is MemberExpression member)
                {
                    VisitMemberExpression(member, andExpression, true);
                }
            }
            else if (binaryExpression.Right is MethodCallExpression call)
            {
                VisitMethodCall(call, andExpression);
            }
            else
            {
                throw new NotSupportedException("Query too complex");
            }
        }

        private AtomicQuery VisitContainsExpression(SubQueryExpression subQuery, bool not = false)
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
                        var oper = not ? QueryOperator.NotIn : QueryOperator.In;

                        var list = new List<KeyValue>();
                        var metadata = GetMetadata(item.Member);

                        foreach (var value in values)
                        {
                            var keyValue = new KeyValue(value, metadata);
                            list.Add(keyValue);
                        }

                        return new AtomicQuery(metadata, list, oper);
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


                        if (value is ConstantExpression valueExpression)
                        {
                            var oper = not ? QueryOperator.NotContains : QueryOperator.Contains;


                            var metadata = GetMetadata(expression.Member);

                            var keyValue = new KeyValue(valueExpression.Value, metadata);


                            return new AtomicQuery(metadata, keyValue, oper);
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
                var andExpression = AndExpression(rootExpression);

                andExpression.Elements.Add(VisitLeafExpression(binaryExpression));
            }
            else
            {
                throw new NotSupportedException("Query too complex");
            }
        }

        private void VisitMemberExpression(MemberExpression expression, OrQuery rootExpression, bool not)
        {
            var andExpression = AndExpression(rootExpression);

            VisitMemberExpression(expression, andExpression, not);
        }

        private void VisitMemberExpression(MemberExpression expression, AndQuery parentExpress, bool not)
        {
            var metadata = GetMetadata(expression.Member);
            var keyValue = new KeyValue(!not, metadata);

            parentExpress.Elements.Add(new AtomicQuery(metadata, keyValue));
        }

        private void VisitMethodCall(MethodCallExpression call, OrQuery rootExpression)
        {
            var andExpression = AndExpression(rootExpression);

            VisitMethodCall(call, andExpression);
        }

        private void VisitMethodCall(MethodCallExpression call, AndQuery andExpression)
        {
            var methodName = call.Method.Name;
            var argument = call.Arguments.FirstOrDefault();

            if (call.Object is MemberExpression member && argument is ConstantExpression constant)
            {
                var value = constant.Value.ToString();


                var metadata = GetMetadata(member.Member);
                var keyValue = new KeyValue(value, metadata);

                QueryOperator op;
                switch (methodName)
                {
                    case "StartsWith":
                        op = QueryOperator.StrStartsWith;
                        break;

                    case "EndsWith":
                        op = QueryOperator.StrEndsWith;
                        break;

                    case "Contains":
                        op = QueryOperator.StrContains;
                        break;

                    default:
                        throw new NotSupportedException($"Method {methodName} can not be used in a query");
                }

                andExpression.Elements.Add(new AtomicQuery(metadata, keyValue, op));
            }
            else
            {
                throw new NotSupportedException($"Error processing method call expression:{methodName}");
            }
        }


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
            else if (binaryExpression.Left.NodeType == ExpressionType.MemberAccess)
            {
                VisitMemberExpression((MemberExpression) binaryExpression.Left, rootExpression, false);
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
                    var atomicQuery = VisitContainsExpression(subQuery);
                    var andQuery = new AndQuery();
                    andQuery.Elements.Add(atomicQuery);
                    rootExpression.Elements.Add(andQuery);
                }
            }
            else if (binaryExpression.Left.NodeType == ExpressionType.Not)
            {
                var unary = (UnaryExpression) binaryExpression.Left;

                if (unary.Operand is SubQueryExpression subQuery)
                {
                    var atomicQuery = VisitContainsExpression(subQuery, true);
                    var andQuery = new AndQuery();
                    andQuery.Elements.Add(atomicQuery);
                    rootExpression.Elements.Add(andQuery);
                }
                else if (unary.Operand is MemberExpression member)
                {
                    VisitMemberExpression(member, rootExpression, true);
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
            else if (binaryExpression.Left is MethodCallExpression call)
            {
                var andExpression = new AndQuery();
                rootExpression.Elements.Add(andExpression);
                VisitMethodCall(call, andExpression);
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
            else if (binaryExpression.Right.NodeType == ExpressionType.MemberAccess)
            {
                VisitMemberExpression((MemberExpression) binaryExpression.Right, rootExpression, false);
            }
            else if (binaryExpression.Right.NodeType == ExpressionType.Extension)
            {
                if (binaryExpression.Right is SubQueryExpression subQuery)
                {
                    var atomicQuery = VisitContainsExpression(subQuery);
                    var andQuery = new AndQuery();
                    andQuery.Elements.Add(atomicQuery);
                    rootExpression.Elements.Add(andQuery);
                }
            }
            else if (binaryExpression.Right.NodeType == ExpressionType.Not)
            {
                var unary = (UnaryExpression) binaryExpression.Right;

                if (unary.Operand is SubQueryExpression subQuery)
                {
                    var atomicQuery = VisitContainsExpression(subQuery, true);
                    var andQuery = new AndQuery();
                    andQuery.Elements.Add(atomicQuery);
                    rootExpression.Elements.Add(andQuery);
                }
                else if (unary.Operand is MemberExpression member)
                {
                    VisitMemberExpression(member, rootExpression, true);
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
            else if (binaryExpression.Right is MethodCallExpression call)
            {
                var andExpression = new AndQuery();
                rootExpression.Elements.Add(andExpression);
                VisitMethodCall(call, andExpression);
            }
            else
            {
                throw new NotSupportedException("Query too complex");
            }
        }

        private static AndQuery AndExpression(OrQuery rootExpression)
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

            return andExpression;
        }


        /// <summary>
        ///     Manage simple expressions like left operator right
        /// </summary>
        /// <param name="binaryExpression"></param>
        private AtomicQuery VisitLeafExpression(BinaryExpression binaryExpression)
        {
            if (binaryExpression.Right is ConstantExpression right)
            {
                if (binaryExpression.Left is MemberExpression left)
                {
                    var metadata = GetMetadata(left.Member);
                    var keyValue = new KeyValue(right.Value, metadata);

                    var oper = ConvertOperator(binaryExpression);

                    return new AtomicQuery(metadata, keyValue, oper);
                }

                if (binaryExpression.Left is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                    if (unary.Operand is MemberExpression member)
                    {
                        var metadata = GetMetadata(member.Member);
                        var keyValue = new KeyValue(right.Value, metadata);

                        var oper = ConvertOperator(binaryExpression);

                        return new AtomicQuery(metadata, keyValue, oper);
                    }
            }


            // try to revert the expression
            {
                var left = binaryExpression.Right as MemberExpression;
                right = binaryExpression.Left as ConstantExpression;

                if (left != null && right != null)
                {
                    var metadata = GetMetadata(left.Member);
                    var keyValue = new KeyValue(right.Value, metadata);

                    var oper = QueryOperator.Eq;

                    // reverse the operator
                    if (binaryExpression.NodeType == ExpressionType.GreaterThan) oper = QueryOperator.Lt;

                    if (binaryExpression.NodeType == ExpressionType.GreaterThanOrEqual) oper = QueryOperator.Le;

                    if (binaryExpression.NodeType == ExpressionType.LessThan) oper = QueryOperator.Gt;

                    if (binaryExpression.NodeType == ExpressionType.LessThanOrEqual) oper = QueryOperator.Ge;

                    return new AtomicQuery(metadata, keyValue, oper);
                }
            }

            throw new NotSupportedException("Error parsing binary expression");
        }

        private static QueryOperator ConvertOperator(BinaryExpression binaryExpression)
        {
            var oper = QueryOperator.Eq;


            if (binaryExpression.NodeType == ExpressionType.GreaterThan) oper = QueryOperator.Gt;

            if (binaryExpression.NodeType == ExpressionType.GreaterThanOrEqual) oper = QueryOperator.Ge;

            if (binaryExpression.NodeType == ExpressionType.LessThan) oper = QueryOperator.Lt;

            if (binaryExpression.NodeType == ExpressionType.LessThanOrEqual) oper = QueryOperator.Le;

            if (binaryExpression.NodeType == ExpressionType.NotEqual) oper = QueryOperator.NotEq;
            return oper;
        }
    }
}