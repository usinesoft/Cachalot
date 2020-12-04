using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Client.Messages
{
    /// <summary>
    ///     Common expression tree manipulation logic
    /// </summary>
    public static class ExpressionTreeHelper
    {
        /// <summary>
        ///     Get the name of the most specific property expressed as an expression tree
        ///     For example t=>t.Product.PremiumLeg.Coupon return "Coupon"
        /// </summary>
        /// <typeparam name="TParent"></typeparam>
        /// <typeparam name="TProperty"></typeparam>
        /// <param name="propertySelector"></param>
        /// <returns></returns>
        public static string PropertyName<TParent, TProperty>(Expression<Func<TParent, TProperty>> propertySelector)
        {
            if (propertySelector == null) throw new ArgumentNullException(nameof(propertySelector));

            if (propertySelector.Body.NodeType == ExpressionType.Convert)
            {
                if (propertySelector.Body is UnaryExpression convert)
                    if (convert.Operand is MemberExpression memberExpression)
                        return memberExpression.Member.Name;
            }
            else
            {
                if (propertySelector.Body is MemberExpression memberExpression) return memberExpression.Member.Name;
            }

            throw new ArgumentException("propertySelector must be a MemberExpression.", nameof(propertySelector));
        }


        private static readonly  Dictionary<string, Func<object, object>> PrecompiledGetters = new Dictionary<string, Func<object, object>>();

        public static Func<object, object> Getter<TEntity>(string name) 
        {
            var key = typeof(TEntity).FullName + "." + name;

            lock (PrecompiledGetters)
            {
                if (PrecompiledGetters.TryGetValue(key,  out var getter))
                {
                    return getter;
                }

                getter = CreateGetter<object>(name);

                PrecompiledGetters[key] = getter;

                return getter;
            }
        }

        public static Action<TEntity, object> CreateSetter<TEntity>(string name) where TEntity: class
        {
            PropertyInfo propertyInfo = typeof(TEntity).GetProperty(name);

            ParameterExpression instance = Expression.Parameter(typeof(TEntity), "instance");
            ParameterExpression propertyValue = Expression.Parameter(typeof(object), "propertyValue");

            var body = Expression.Assign(Expression.Property(instance, name), propertyValue);

            return Expression.Lambda<Action<TEntity, object>>(body, instance, propertyValue).Compile();
        }

        public static Func<TEntity, object> CreateGetter<TEntity>(string name) 
        {
            ParameterExpression instance = Expression.Parameter(typeof(TEntity), "instance");

            var body = Expression.Property(instance, name);

            return Expression.Lambda<Func<TEntity, object>>(body, instance).Compile();
        }



    }

    
}