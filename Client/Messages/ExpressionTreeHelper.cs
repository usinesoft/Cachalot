using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Client.Core;
using Client.Core.Linq;
using Client.Queries;
using Client.Tools;
using JetBrains.Annotations;

namespace Client.Messages;

/// <summary>
///     Common expression tree manipulation logic
/// </summary>
public static class ExpressionTreeHelper
{
    private static readonly Dictionary<string, Func<object, object>> PrecompiledGetters = new();


    private static readonly Dictionary<Type, List<Func<object, object>>> StringGetterCache = new();

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

    public static Func<object, object> Getter<TEntity>(string name)
    {
        return Getter(typeof(TEntity), name);
    }

    public static Func<object, object> Getter(Type declaringType, string name)
    {
        var key = declaringType.FullName + "." + name;

        lock (PrecompiledGetters)
        {
            if (PrecompiledGetters.TryGetValue(key, out var getter)) return getter;

            getter = CompileGetter(declaringType, name);

            PrecompiledGetters[key] = getter;

            return getter;
        }
    }


    private static Func<object, object> CompileGetter(Type declaringType, string propertyName)
    {
        var instance = Expression.Parameter(typeof(object), "instance");


        var propertyInfo = declaringType.GetProperty(propertyName);
        if (propertyInfo == null)
            throw new NotSupportedException($"Can not find property {propertyName} of type{declaringType.FullName}");

        var instanceCast = declaringType.IsValueType
            ? Expression.TypeAs(instance, declaringType)
            : Expression.Convert(instance, declaringType);

        return
            Expression.Lambda<Func<object, object>>(
                    Expression.TypeAs(Expression.Call(instanceCast, propertyInfo.GetGetMethod()), typeof(object)),
                    instance)
                .Compile();
    }


    public static OrQuery PredicateToQuery<T>(Expression<Func<T, bool>> where, string collectionName = null)
    {
        var schema = TypeDescriptionsCache.GetDescription(typeof(T));
        collectionName ??= schema.CollectionName;

        // create a fake queryable to force query parsing and capture resolution
        var executor = new NullExecutor(schema, collectionName);
        var queryable = new NullQueryable<T>(executor);

        var unused = queryable.Where(where).ToList();

        var query = executor.Expression;
        query.CollectionName = collectionName ?? typeof(T).FullName;

        return query;
    }

    /// <summary>
    ///     Generate precompiled getters for a type. This will avoid using reflection for each call
    /// </summary>
    /// <param name="type"></param>
    private static void GenerateAccessorsForType([NotNull] Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));

        var accessors = new List<Func<object, object>>();
        foreach (var property in type.GetProperties())
            if (property.PropertyType == typeof(string))
                accessors.Add(property.CompileGetter());

        StringGetterCache[type] = accessors;
    }


    /// <summary>
    ///     Used for full-text search
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="instance"></param>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    public static IList<string> GetStringValues<TEntity>(TEntity instance, string propertyName)
    {
        var result = new List<string>();

        if (instance == null)
            throw new ArgumentNullException(nameof(instance));


        var value = Getter<TEntity>(propertyName)(instance);

        if (value is string text
           ) // string is also an IEnumerable, but we do not want to be processed as a collection
        {
            result.Add(text);
        }
        else if (value is IEnumerable values)
        {
            foreach (var val in values) result.AddRange(ToStrings(val));
        }
        else
        {
            if (value != null) result.AddRange(ToStrings(value));
        }


        return result;
    }

    /// <summary>
    ///     Return a list off all the values of string properties
    /// </summary>
    /// <param name="instance"></param>
    /// <returns></returns>
    private static IList<string> ToStrings(object instance)
    {
        var result = new List<string>();
        var type = instance.GetType();

        if (type == typeof(string))
        {
            result.Add((string)instance);
        }
        else if (type.Namespace != null && type.Namespace.StartsWith("System"))
        {
            result.Add(instance.ToString());
        }
        else // some complex type
        {
            List<Func<object, object>> accessors;
            lock (StringGetterCache)
            {
                if (!StringGetterCache.ContainsKey(type)) GenerateAccessorsForType(type);
                accessors = StringGetterCache[type];
            }

            if (accessors != null)
                foreach (var accessor in accessors)
                {
                    var val = accessor(instance);
                    if (val != null) result.Add(val as string);
                }
        }


        return result;
    }
}