using System.Linq;
using System.Linq.Expressions;
using Remotion.Linq;
using Remotion.Linq.Parsing.Structure;
// ReSharper disable UnusedMember.Global

namespace Cachalot.Linq
{

    /// <summary>
    /// Dummy queryable used to parse queries without executing them
    /// </summary>
    /// <typeparam name="T"></typeparam>
    class NullQueryable<T> : QueryableBase<T>
    {
        
        public NullQueryable(IQueryParser queryParser, IQueryExecutor executor) : base(queryParser, executor)
        {
        }

        public NullQueryable(IQueryProvider provider) : base(provider)
        {
        }

        public NullQueryable(IQueryProvider provider, Expression expression) : base(provider, expression)
        {
        }

        public NullQueryable(IQueryExecutor executor): base(QueryParser.CreateDefault(), executor)
        {

        }
    }
}