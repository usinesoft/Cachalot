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
        ///     Between
        /// </summary>
        Btw,

        /// <summary>
        ///     Applies to list keys
        /// </summary>
        In
    }
}