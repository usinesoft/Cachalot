using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Client.Core.Linq
{
    public static class MyQueryExtensions

    {
        public static IQueryable<T> FullTextSearch<T>(
            this IQueryable<T> source,
            string query)

        {
            return source.Provider.CreateQuery<T>(
                Expression.Call(
                    ((MethodInfo)MethodBase.GetCurrentMethod())
                    .MakeGenericMethod(typeof(T)),
                    source.Expression,
                    Expression.Constant(query)));
        }


        public static IQueryable<T> OnlyIfComplete<T>(
            this IQueryable<T> source)

        {
            return source.Provider.CreateQuery<T>(
                Expression.Call(
                    ((MethodInfo)MethodBase.GetCurrentMethod())
                    .MakeGenericMethod(typeof(T)),
                    source.Expression));
        }
    }
}