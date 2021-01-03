using Server.Persistence;

namespace Server
{
    public class Services
    {
        /// <summary>
        /// Singleton
        /// </summary>
        public ILockManager LockManager { get; } = new LockManager();

        public PersistenceEngine PersistenceEngine { get; } = new PersistenceEngine();

    }
}