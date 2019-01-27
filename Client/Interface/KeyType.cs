namespace Client.Interface
{
    /// <summary>
    ///     Uniqueness of the key
    /// </summary>
    public enum KeyType
    {
        /// <summary>
        ///     internal use only
        /// </summary>
        None,

        /// <summary>
        ///     the one and only primary key of a type
        /// </summary>
        Primary,

        /// <summary>
        ///     unique and non null value
        /// </summary>
        Unique,

        /// <summary>
        ///     non unique value (can be null)
        /// </summary>
        ScalarIndex,

        /// <summary>
        ///     Indexed list of values
        /// </summary>
        ListIndex
    }
}