using Server.Persistence;
using Server.Queries;

namespace Server
{
    public class Services
    {
        /// <summary>
        /// Singleton
        /// </summary>
        public ILockManager LockManager { get; } = new LockManager();

        /// <summary>
        /// Singleton
        /// </summary>
        public IFeedSessionManager FeedSessionManager {get;} = new FeedSessionManager();

        public PersistenceEngine PersistenceEngine { get; } = new PersistenceEngine();

    }
}