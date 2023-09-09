using System.Collections.Generic;
using System.Linq;
using Client.Queries;
using Remotion.Linq;

namespace Client.Core.Linq;

public class NullExecutor : IQueryExecutor
{
    private readonly string _collectionName;
    private readonly CollectionSchema _collectionSchema;

    public NullExecutor(CollectionSchema collectionSchema, string collectionName)
    {
        _collectionSchema = collectionSchema;
        _collectionName = collectionName;
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
        var visitor = new QueryVisitor(_collectionName, _collectionSchema);

        visitor.VisitQueryModel(queryModel);

        Expression = visitor.RootExpression;

        return Enumerable.Empty<T>();
    }
}