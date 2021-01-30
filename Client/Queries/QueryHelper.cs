using System.Linq;
using Client.Interface;

namespace Client.Queries
{
    public static class QueryHelper
    {
        public static void OptimizeQuery(OrQuery rootExpression)
        {
            // convert < and > into BETWEEN operator. Much more efficient

            foreach (var andQuery in rootExpression.Elements)
            {
                var multipleTests = andQuery.Elements.Where(q=>q.IsComparison).GroupBy(q => q.PropertyName).Where(g => g.Count() > 1).ToList();

                if (multipleTests.Count > 0)
                {
                    // these ones will not be changed
                    var atomicQueries = andQuery.Elements.Where(q => multipleTests.All(mt => mt.Key != q.PropertyName))
                        .ToList();

                    foreach (var multipleTest in multipleTests)
                    {
                        if (multipleTest.Count() == 2)
                        {

                            var q1 = multipleTest.First();
                            var q2 = multipleTest.Last();

                            // multiple atomic queries for the same index do not make sense
                            if (q1.Operator == QueryOperator.Eq)
                                throw new CacheException($"Inconsistent query on index {multipleTest.Key}");

                            if (q2.Operator == QueryOperator.Eq)
                                throw new CacheException($"Inconsistent query on index {multipleTest.Key}");


                            var optimized = false;

                            // a >= x && a <=y will be converted to "a BETWEEN x, y"

                            if (q1.Operator != QueryOperator.In && q1.Operator != QueryOperator.In)
                            {
                                if (q1.Value < q2.Value)
                                {
                                    QueryOperator oper = QueryOperator.Eq;
                                    if (q1.Operator == QueryOperator.Ge)
                                        if (q2.Operator == QueryOperator.Le)
                                        {
                                            oper = QueryOperator.GeLe;
                                        }
                                    if (q1.Operator == QueryOperator.Gt)
                                        if (q2.Operator == QueryOperator.Le)
                                        {
                                            oper = QueryOperator.GtLe;
                                        }
                                    if (q1.Operator == QueryOperator.Gt)
                                        if (q2.Operator == QueryOperator.Lt)
                                        {
                                            oper = QueryOperator.GtLt;
                                        }
                                    if (q1.Operator == QueryOperator.Ge)
                                        if (q2.Operator == QueryOperator.Lt)
                                        {
                                            oper = QueryOperator.GeLt;
                                        }

                                    if (oper.IsRangeOperator())
                                    {
                                        var between = new AtomicQuery(q1.Metadata,  q1.Value, q2.Value, oper);
                                        atomicQueries.Add(between);
                                        optimized = true;
                                    }
                                    
                                }
                                else if (q1.Value > q2.Value)
                                {
                                    QueryOperator oper = QueryOperator.Eq;

                                    if (q1.Operator == QueryOperator.Le)
                                        if (q2.Operator == QueryOperator.Ge)
                                        {
                                            oper = QueryOperator.GeLe;
                                        }
                                    if (q1.Operator == QueryOperator.Lt)
                                        if (q2.Operator == QueryOperator.Ge)
                                        {
                                            oper = QueryOperator.GeLt;
                                        }
                                    if (q1.Operator == QueryOperator.Le)
                                        if (q2.Operator == QueryOperator.Gt)
                                        {
                                            oper = QueryOperator.GtLe;
                                        }
                                    if (q1.Operator == QueryOperator.Lt)
                                        if (q2.Operator == QueryOperator.Gt)
                                        {
                                            oper = QueryOperator.GtLt;
                                        }

                                    var between = new AtomicQuery(q1.Metadata, q2.Value, q1.Value, oper);
                                    atomicQueries.Add(between);
                                    optimized = true;
                                }
                            }

                            if (!optimized)
                            {
                                // keep the original expressions 
                                atomicQueries.Add(q1);
                                atomicQueries.Add(q2);
                            }
                        }
                            

                        
                    }

                    andQuery.Elements.Clear();
                    andQuery.Elements.AddRange(atomicQueries);
                }
            }
        }
    }
}