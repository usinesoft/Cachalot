using System.Collections.Generic;
using System.Linq;
using Client.Messages;
using Client.Queries;
using Remotion.Linq;

namespace Client.Core.Linq
{
    public class NullExecutor : IQueryExecutor
    {
        private readonly CollectionSchema _collectionSchema;

        public NullExecutor(CollectionSchema collectionSchema)
        {
            _collectionSchema = collectionSchema;
        }

        public OrQuery Expression { get; private set; }

        public T ExecuteScalar<T>(QueryModel queryModel)
        {
            return default;
        }

        public T ExecuteSingle<T>(QueryModel queryModel, bool returnDefaultWhenEmpty)
        {
            return default;
        }

        public IEnumerable<T> ExecuteCollection<T>(QueryModel queryModel)
        {
            var visitor = new QueryVisitor(_collectionSchema);

            visitor.VisitQueryModel(queryModel);

            Expression = visitor.RootExpression;

            return Enumerable.Empty<T>();
        }
    }
}