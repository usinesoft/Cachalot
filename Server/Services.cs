using Server.Persistence;
using Server.Queries;

namespace Server;

public class Services
{
    public Services(ILog log, INodeConfig nodeConfig)
    {
        Log = log;
        NodeConfig = nodeConfig;

        SchemaPersistence = new SchemaPersistence(NodeConfig);

        SequencePersistence = new SequencePersistence(NodeConfig);
    }

    public ILog Log { get; }
    public INodeConfig NodeConfig { get; }

    /// <summary>
    ///     Singleton
    /// </summary>
    public ILockManager LockManager { get; } = new LockManager();

    /// <summary>
    ///     Singleton
    /// </summary>
    public IFeedSessionManager FeedSessionManager { get; } = new FeedSessionManager();

    public PersistenceEngine PersistenceEngine { get; } = new();

    public ISchemaPersistence SchemaPersistence { get; }
    public ISequencePersistence SequencePersistence { get; }
}