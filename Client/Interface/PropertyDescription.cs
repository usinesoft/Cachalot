namespace Client.Interface
{
    /// <summary>
    ///     Explicit description of a property from a cacheable type
    /// </summary>
    public class PropertyDescription
    {

        public bool ServerSideVisible { get; set; }

        /// <summary>
        /// </summary>
        /// <param name="propertyName">name of the property</param>
        /// <param name="keyType">primary, unique, index, none</param>
        /// <param name="keyDataType">integer or string</param>
        /// <param name="ordered">create ordered index for this property</param>
        /// <param name="fullTextIndexed">create full-text index for this property</param>
        /// <param name="serverSideValue">can the property be used for server-side aggregation</param>
        public PropertyDescription(string propertyName, KeyType keyType, KeyDataType keyDataType, bool ordered,
            bool fullTextIndexed, bool serverSideValue)
        {
            PropertyName = propertyName;
            Ordered = ordered;
            KeyType = keyType;
            KeyDataType = keyDataType;
            FullTextIndexed = fullTextIndexed;
            ServerSideVisible = serverSideValue;
        }

        public bool FullTextIndexed { get; }
        
        
        /// <summary>
        ///     Case sensitive name of the property in the type
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        ///     If true comparision operators may be used when querying the cache on this property
        /// </summary>
        public bool Ordered { get; }

        /// <summary>
        ///     Primary/Unique/Index
        /// </summary>
        public KeyType KeyType { get; }

        /// <summary>
        ///     The keys are always stored internally as string or long
        /// </summary>
        public KeyDataType KeyDataType { get; }

        
    }
}