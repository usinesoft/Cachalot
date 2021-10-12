namespace Client.Core
{
    public enum IndexType
    {
        /// <summary>
        /// Not indexed. A property that can be used in queries but with sub-optimal performance
        /// </summary>
        None,
        /// <summary>
        /// The one and unique primary key
        /// </summary>
        Primary,
        
        /// <summary>
        /// Dictionary index (fast search but the comparison operators are not indexed)
        /// </summary>
        Dictionary,
        /// <summary>
        /// Ordered index (fast even with comparison operator but a little slower on insertions)
        /// </summary>
        Ordered
    }
}