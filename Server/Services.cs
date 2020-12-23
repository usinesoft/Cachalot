namespace Server
{
    public class Services
    {
        /// <summary>
        /// Singleton
        /// </summary>
        public ILockManager LockManager { get; } = new LockManager();
    }
}