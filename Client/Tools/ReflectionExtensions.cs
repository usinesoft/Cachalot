using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace Client.Tools;

/// <summary>
///     Precompile accessors to avoid reflection at runtime
/// </summary>
public static class ReflectionExtensions
{
    public static Func<object, object> CompileGetter(this PropertyInfo propertyInfo)
    {
        var instance = Expression.Parameter(typeof(object), "instance");

        Debug.Assert(propertyInfo.DeclaringType != null, "propertyInfo.DeclaringType != null");
        var instanceCast = propertyInfo.DeclaringType.IsValueType
            ? Expression.TypeAs(instance, propertyInfo.DeclaringType)
            : Expression.Convert(instance, propertyInfo.DeclaringType);

        return
            Expression.Lambda<Func<object, object>>(
                    Expression.TypeAs(Expression.Call(instanceCast, propertyInfo.GetGetMethod()), typeof(object)),
                    instance)
                .Compile();
    }
}