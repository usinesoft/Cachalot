using Server.Persistence;
using Server.Queries;

namespace Server
{
    public class Services
    {
        public ILog Log { get; }
        public INodeConfig NodeConfig { get; }

        public Services(ILog log, INodeConfig nodeConfig)
        {
            Log = log;
            NodeConfig = nodeConfig;
            SchemaPersistence = new SchemaPersistence(NodeConfig);
        }

        /// <summary>
        /// Singleton
        /// </summary>
        public ILockManager LockManager { get; } = new LockManager();

        /// <summary>
        /// Singleton
        /// </summary>
        public IFeedSessionManager FeedSessionManager {get;} = new FeedSessionManager();

        public PersistenceEngine PersistenceEngine { get; } = new PersistenceEngine();

        public ISchemaPersistence SchemaPersistence { get; } 

    }
}