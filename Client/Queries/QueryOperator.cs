namespace Client.Queries
{
    /// <summary>
    ///     Operators to be used in <see cref="AtomicQuery" />
    ///     On the server side these operators can be directly applied to indexes
    /// </summary>
    public enum QueryOperator
    {
        /// <summary>
        ///     Equality operator(==)
        /// </summary>
        Eq,

        /// <summary>
        ///     Greater
        /// </summary>
        Gt,

        /// <summary>
        ///     Greater or Equal
        /// </summary>
        Ge,

        /// <summary>
        ///     Less
        /// </summary>
        Lt,

        /// <summary>
        ///     Less or equal
        /// </summary>
        Le,

        /// <summary>
        ///     [Value, Value2]
        /// </summary>
        GeLe,

        /// <summary>
        ///     (Value, Value2]
        /// </summary>
        GtLe,

        /// <summary>
        ///     (Value, Value2)
        /// </summary>
        GtLt,
        /// <summary>
        ///     [Value, Value2)
        /// </summary>
        GeLt,

        /// <summary>
        ///     Value in list of values (the query has one value, it applies to a list of values)
        /// </summary>
        In,

        /// <summary>
        ///  Values contain value (the query has a list of values, it applies to a scalar value)
        /// </summary>
        Contains,

        /// <summary>
        /// Not equal
        /// </summary>
        NotEq,

        /// <summary>
        /// Value not in list of values (the query has one value, it applies to a list of values)
        /// </summary>
        NotIn,

        /// <summary>
        /// Values do not contain value (the query has a list of values, it applies to a scalar value)
        /// </summary>
        NotContains,

        /// <summary>
        /// StartsWith string method
        /// </summary>
        StrStartsWith, 

        /// <summary>
        /// EndsWith string method
        /// </summary>
        StrEndsWith, 

        /// <summary>
        /// EndsWith string method
        /// </summary>
        StrContains, 
    }

    public static class OperatorExtensions
    {
        public static bool IsRangeOperator(this QueryOperator @this)
        {
            if (@this == QueryOperator.GeLe || @this == QueryOperator.GeLt || @this == QueryOperator.GtLt ||
                @this == QueryOperator.GtLe)
                return true;


            return false;
        }
    }
    
}