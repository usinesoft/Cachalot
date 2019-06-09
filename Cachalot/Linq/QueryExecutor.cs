using System;
using System.Collections.Generic;
using System.Linq;
using Client;
using Client.Interface;
using Client.Messages;
using Client.Queries;
using Remotion.Linq;

namespace Cachalot.Linq
{
    internal class QueryExecutor : IQueryExecutor
    {
        private static Action<OrQuery> _customAction;

        private readonly ICacheClient _client;
        private readonly TypeDescription _typeDescription;


        public QueryExecutor(ICacheClient client, TypeDescription typeDescription)
        {
            _client = client;
            _typeDescription = typeDescription;
        }

        // Executes a query with a scalar result, i.e. a query that ends with a result operator such as Count, Sum, or Average.
        public T ExecuteScalar<T>(QueryModel queryModel)
        {
            var visitor = new QueryVisitor(_typeDescription);

            visitor.VisitQueryModel(queryModel);

            var expression = visitor.RootExpression;

            _customAction?.Invoke(expression);

            Dbg.Trace($"linq provider produced expression {expression}");

            if (expression.CountOnly) return (T) (object) _client.EvalQuery(expression).Value;

            throw new NotSupportedException("Only Count scalar method is implemented");
        }

        // Executes a query with a single result object, i.e. a query that ends with a result operator such as First, Last, Single, Min, or Max.
        public T ExecuteSingle<T>(QueryModel queryModel, bool returnDefaultWhenEmpty)
        {
            return returnDefaultWhenEmpty
                ? ExecuteCollection<T>(queryModel).SingleOrDefault()
                : ExecuteCollection<T>(queryModel).Single();
        }

        // Executes a query with a collection result.
        public IEnumerable<T> ExecuteCollection<T>(QueryModel queryModel)
        {
            var visitor = new QueryVisitor(_typeDescription);

            visitor.VisitQueryModel(queryModel);

            var expression = visitor.RootExpression;

            _customAction?.Invoke(expression);

            Dbg.Trace($"linq provider produced expression {expression}");

            return _client.GetMany<T>(visitor.RootExpression);
        }

        public static void Probe(Action<OrQuery> action)
        {
            _customAction = action;
        }
    }
}