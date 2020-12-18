using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Client.Interface;
using Client.Messages;
using Client.Queries;
using Remotion.Linq;
using Remotion.Linq.Clauses;
using Remotion.Linq.Clauses.Expressions;
using Remotion.Linq.Clauses.ResultOperators;

namespace Client.Core.Linq
{
    public class QueryVisitor : QueryModelVisitorBase
    {
        private readonly CollectionSchema _collectionSchema;

        public QueryVisitor(CollectionSchema collectionSchema)
        {
            _collectionSchema = collectionSchema ?? throw new ArgumentNullException(nameof(collectionSchema));
            RootExpression = new OrQuery(_collectionSchema);
        }

        public OrQuery RootExpression { get; }


        public override void VisitQueryModel(QueryModel queryModel)
        {
            base.VisitQueryModel(queryModel);

            QueryHelper.OptimizeQuery(RootExpression);
        }

        private KeyValue AsKeyValue(MemberInfo member, object value)
        {
            var propertyDescription = _collectionSchema.KeyByName(member.Name);
            if (propertyDescription == null)
            {
                throw new CacheException($"property {member.Name} is not servers-side visible");
            }

            var keyInfo = new KeyInfo(propertyDescription.Name, propertyDescription.Order,
                propertyDescription.IndexType);


            return new KeyValue(value, keyInfo);
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

            if(whereClause.Predicate.NodeType == ExpressionType.Not)
            {
                var unary = (UnaryExpression) whereClause.Predicate;
                InternalVisitWhereClause(unary.Operand, true);    
            }
            else
            {
                InternalVisitWhereClause(whereClause.Predicate,  false);    
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
                {
                    // Generalize for more complex expressions
                    VisitMemberExpression(memberExpression, RootExpression, not);
                }
            }
            else if (whereClause is MethodCallExpression call)
            {
                VisitMethodCall(call, RootExpression);

            }
            else
            {
                if (whereClause is SubQueryExpression subQuery)
                {
                    var leaf = CreateAtomicQuery(RootExpression);

                    VisitContainsExpression(subQuery, leaf, not);
                }
                else
                {
                    throw new NotSupportedException("Incorrect query");
                }
            }


            RootExpression.MultipleWhereClauses = true;

            //base.VisitWhereClause(whereClause, queryModel, index);
        }

        public override void VisitSelectClause(SelectClause selectClause, QueryModel queryModel)
        {
            var expression = selectClause.Selector;
            if (expression is MemberExpression member)
            {
                RootExpression.SelectedProperties.Add(member.Member.Name);
            }
            else if(expression is NewExpression @new)
            {
                foreach (var memberInfo in @new.Members)
                {
                    RootExpression.SelectedProperties.Add(memberInfo.Name);
                }
            }
            
            base.VisitSelectClause(selectClause, queryModel);
        }

        public override void VisitOrderByClause(OrderByClause orderByClause, QueryModel queryModel, int index)
        {
            base.VisitOrderByClause(orderByClause, queryModel, index);
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
            else if(binaryExpression.Left.NodeType == ExpressionType.MemberAccess)
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
                    var leaf = new AtomicQuery();
                    andExpression.Elements.Add(leaf);
                    VisitContainsExpression(subQuery, leaf);
                }
            }
            else if (binaryExpression.Left.NodeType == ExpressionType.Not)
            {
                var unary = (UnaryExpression) binaryExpression.Left;

                if (unary.Operand is SubQueryExpression subQuery)
                {
                    var leaf = new AtomicQuery();
                    andExpression.Elements.Add(leaf);
                    VisitContainsExpression(subQuery, leaf, true);
                }
                else if (unary.Operand is MemberExpression member)
                {
                    VisitMemberExpression(member, andExpression, true);
                }
            }
            else if(binaryExpression.Left is MethodCallExpression call)
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
            else if(binaryExpression.Right.NodeType == ExpressionType.MemberAccess)
            {
                VisitMemberExpression((MemberExpression) binaryExpression.Right, andExpression, false);
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
            else if (binaryExpression.Right.NodeType == ExpressionType.Not)
            {
                var unary = (UnaryExpression) binaryExpression.Right;

                if (unary.Operand is SubQueryExpression subQuery)
                {
                    var leaf = new AtomicQuery();
                    andExpression.Elements.Add(leaf);
                    VisitContainsExpression(subQuery, leaf, true);
                }
                else if (unary.Operand is MemberExpression member)
                {
                    VisitMemberExpression(member, andExpression, true);
                }
            }
            else if(binaryExpression.Right is MethodCallExpression call)
            {
                VisitMethodCall(call, andExpression);
            }
            else
            {
                throw new NotSupportedException("Query too complex");
            }
        }

        private void VisitContainsExpression(SubQueryExpression subQuery, AtomicQuery leaf, bool not = false)
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
                        leaf.Operator = not? QueryOperator.Nin :QueryOperator.In;

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


                        if (value is ConstantExpression valueExpression)
                        {
                            leaf.Operator = not? QueryOperator.Nin :QueryOperator.In;

                            var kval = AsKeyValue(expression.Member, valueExpression.Value);

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
            var kval = AsKeyValue(expression.Member, !not);

            parentExpress.Elements.Add( new AtomicQuery(kval));

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

                var kval = AsKeyValue(member.Member, value);

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

                andExpression.Elements.Add( new AtomicQuery(kval, op));
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
            else if(binaryExpression.Left.NodeType == ExpressionType.MemberAccess)
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
                    var leaf = CreateAtomicQuery(rootExpression);

                    VisitContainsExpression(subQuery, leaf);
                }
            }
            else if (binaryExpression.Left.NodeType == ExpressionType.Not)
            {
                var unary = (UnaryExpression) binaryExpression.Left;

                if (unary.Operand is SubQueryExpression subQuery)
                {
                    var leaf = CreateAtomicQuery(rootExpression);

                    VisitContainsExpression(subQuery, leaf, true);
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
            else if(binaryExpression.Left is MethodCallExpression call)
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
            else if(binaryExpression.Right.NodeType == ExpressionType.MemberAccess)
            {
                VisitMemberExpression((MemberExpression) binaryExpression.Right, rootExpression, false);
            }
            else if (binaryExpression.Right.NodeType == ExpressionType.Extension)
            {
                if (binaryExpression.Right is SubQueryExpression subQuery)
                {
                    var leaf = CreateAtomicQuery(rootExpression);

                    VisitContainsExpression(subQuery, leaf);
                }
            }
            else if (binaryExpression.Right.NodeType == ExpressionType.Not)
            {
                var unary = (UnaryExpression) binaryExpression.Right;

                if (unary.Operand is SubQueryExpression subQuery)
                {
                    var leaf = CreateAtomicQuery(rootExpression);

                    VisitContainsExpression(subQuery, leaf, true);
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
            else if(binaryExpression.Right is MethodCallExpression call)
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

        private static AtomicQuery CreateAtomicQuery(OrQuery rootExpression)
        {
            var andExpression = AndExpression(rootExpression);

            var leaf = new AtomicQuery();
            andExpression.Elements.Add(leaf);
            return leaf;
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
            if (binaryExpression.Left is MemberExpression left && binaryExpression.Right is ConstantExpression right)
            {
                var kval = AsKeyValue(left.Member, right.Value);

                var oper = QueryOperator.Eq;


                if (binaryExpression.NodeType == ExpressionType.GreaterThan) oper = QueryOperator.Gt;

                if (binaryExpression.NodeType == ExpressionType.GreaterThanOrEqual) oper = QueryOperator.Ge;

                if (binaryExpression.NodeType == ExpressionType.LessThan) oper = QueryOperator.Lt;

                if (binaryExpression.NodeType == ExpressionType.LessThanOrEqual) oper = QueryOperator.Le;

                if (binaryExpression.NodeType == ExpressionType.NotEqual) oper = QueryOperator.Neq;

                return new AtomicQuery(kval, oper);
            }

            // try to revert the expression
            left = binaryExpression.Right as MemberExpression;
            right = binaryExpression.Left as ConstantExpression;

            if (left != null && right != null)
            {
                var kval = AsKeyValue(left.Member, right.Value);

                var oper = QueryOperator.Eq;


                if (binaryExpression.NodeType == ExpressionType.GreaterThan) oper = QueryOperator.Lt;

                if (binaryExpression.NodeType == ExpressionType.GreaterThanOrEqual) oper = QueryOperator.Le;

                if (binaryExpression.NodeType == ExpressionType.LessThan) oper = QueryOperator.Gt;

                if (binaryExpression.NodeType == ExpressionType.LessThanOrEqual) oper = QueryOperator.Ge;

                return new AtomicQuery(kval, oper);
            }

            throw new NotSupportedException("Error parsing binary expression");
        }
    }
}