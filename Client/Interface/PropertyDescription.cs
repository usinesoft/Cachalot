namespace Client.Interface
{
    /// <summary>
    ///     Explicit description of a property from a cacheable type
    /// </summary>
    public class PropertyDescription
    {
        /// <summary>
        /// </summary>
        public PropertyDescription()
        {
        }


        /// <summary>
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="keyType"></param>
        /// <param name="keyDataType"></param>
        /// <param name="ordered"></param>
        /// <param name="fullTextIndexed"></param>
        public PropertyDescription(string propertyName, KeyType keyType, KeyDataType keyDataType, bool ordered,
            bool fullTextIndexed)
        {
            PropertyName = propertyName;
            Ordered = ordered;
            KeyType = keyType;
            KeyDataType = keyDataType;
            FullTextIndexed = fullTextIndexed;
        }

        public bool FullTextIndexed { get; set; }

        /// <summary>
        ///     Case sensitive name of the property in the type
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        ///     If true comparision operators may be used when querying the cache on this property
        /// </summary>
        public bool Ordered { get; set; }

        /// <summary>
        ///     Primary/Unique/Index
        /// </summary>
        public KeyType KeyType { get; set; }

        /// <summary>
        ///     The keys are always stored internally as string or long
        /// </summary>
        public KeyDataType KeyDataType { get; set; }
    }
}