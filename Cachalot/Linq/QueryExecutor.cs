using System;
using System.Collections.Generic;
using System.Linq;
using Client;
using Client.Core;
using Client.Core.Linq;
using Client.Interface;
using Client.Queries;
using Newtonsoft.Json.Linq;
using Remotion.Linq;

namespace Cachalot.Linq
{
    internal class QueryExecutor : IQueryExecutor
    {
        private static Action<OrQuery> _customAction;

        private readonly IDataClient _client;
        private readonly CollectionSchema _collectionSchema;
        private readonly Guid _sessionId;
        private readonly string _collectionName;


        public QueryExecutor(IDataClient client, CollectionSchema collectionSchema, Guid sessionId, string collectionName)
        {
            _client = client;
            _collectionSchema = collectionSchema;
            _sessionId = sessionId;
            _collectionName = collectionName;
        }

        // Executes a query with a scalar result, i.e. a query that ends with a result operator such as Count, Sum, or Average.
        public T ExecuteScalar<T>(QueryModel queryModel)
        {
            var visitor = new QueryVisitor( _collectionName, _collectionSchema);

            visitor.VisitQueryModel(queryModel);

            var expression = visitor.RootExpression;

            _customAction?.Invoke(expression);

            Dbg.Trace($"linq provider produced expression {expression}");

            if (expression.CountOnly) return (T) (object) _client.EvalQuery(expression).Item2;

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
            var visitor = new QueryVisitor(_collectionName, _collectionSchema);

            visitor.VisitQueryModel(queryModel);

            var expression = visitor.RootExpression;

            _customAction?.Invoke(expression);

            Dbg.Trace($"linq provider produced expression {expression}");

            return _client.GetMany(visitor.RootExpression, _sessionId).Select(ri=>FromJObject<T>(ri.Item));
        }

        T FromJObject<T>(JObject jObject)
        {
            var t = typeof(T);
            bool isPrimitiveType = t.IsPrimitive || t.IsValueType || (t == typeof(string)||t == typeof(DateTime))||t == typeof(DateTimeOffset)||t == typeof(decimal);

            if (jObject.Count == 1)
            {
                return jObject.Properties().First().Value.ToObject<T>();
            }

            return jObject.ToObject<T>(SerializationHelper.Serializer);
        }

        public static void Probe(Action<OrQuery> action)
        {
            _customAction = action;
        }
    }
}