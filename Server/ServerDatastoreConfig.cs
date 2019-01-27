namespace Server
{
    /// <summary>
    ///   Configuration of a <see cref="DataStore" />
    /// </summary>
    public class ServerDatastoreConfig
    {
        /// <summary>
        ///   Eviction configuration for this <see cref="DataStore" />
        ///   By default the eviction is not activated
        /// </summary>
        private EvictionPolicyConfig _eviction = new EvictionPolicyConfig();

        /// <summary>
        ///   Number of threads prestarted for this <see cref="DataStore" />.
        /// </summary>
        private int _threads = 4;

        /// <summary>
        ///   The name of the .NET type of the <see cref="DataStore" />
        /// </summary>
        public string FullTypeName { get; set; }

        /// <summary>
        ///   Number of threads prestarted for this <see cref="DataStore" />.
        /// </summary>
        public int Threads
        {
            get { return _threads; }
            set { _threads = value; }
        }

        /// <summary>
        ///   Eviction configuration for this <see cref="DataStore" />
        /// </summary>
        public EvictionPolicyConfig Eviction
        {
            get { return _eviction; }
            set { _eviction = value; }
        }
    }
}