using System;
using System.Linq.Expressions;

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
    }
}