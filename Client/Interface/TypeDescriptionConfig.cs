using System.Collections.Generic;

namespace Client.Interface
{
    /// <summary>
    ///     Define the index-able properties and the storage parameters(compression) of a type
    /// </summary>
    public class TypeDescriptionConfig
    {
        private readonly Dictionary<string, PropertyDescription> _keys =
            new Dictionary<string, PropertyDescription>();

        /// <summary>
        ///     Property description by property name
        /// </summary>
        public IDictionary<string, PropertyDescription> Keys => _keys;

        public string FullTypeName { get; set; }

        public string AssemblyName { get; set; }

        public bool UseCompression { get; set; }


        /// <summary>
        ///     Helper method. Directly add a property description
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="keyType"></param>
        /// <param name="keyDataType"></param>
        /// <param name="ordered"></param>
        /// <param name="fullTextIndexed"></param>
        /// <param name="serverSideVisible"></param>
        public void Add(string propertyName, KeyType keyType, KeyDataType keyDataType, bool ordered = false,
            bool fullTextIndexed = false, bool serverSideVisible = false)
        {
            _keys.Add(propertyName,
                new PropertyDescription(propertyName, keyType, keyDataType, ordered, fullTextIndexed, serverSideVisible));
        }
    }
}