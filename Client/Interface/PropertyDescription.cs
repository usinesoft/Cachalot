namespace Client.Interface
{
    /// <summary>
    ///     Explicite description of a property from a cacheable type
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
        public PropertyDescription(string propertyName, KeyType keyType, KeyDataType keyDataType)
            : this(propertyName, keyType, keyDataType, false)
        {
        }

        /// <summary>
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="keyType"></param>
        /// <param name="keyDataType"></param>
        /// <param name="order"></param>
        public PropertyDescription(string propertyName, KeyType keyType, KeyDataType keyDataType, bool order)
        {
            PropertyName = propertyName;
            Ordered = order;
            KeyType = keyType;
            KeyDataType = keyDataType;
        }

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
        ///     The keys are allways stored internally as string or long
        /// </summary>
        public KeyDataType KeyDataType { get; set; }
    }
}