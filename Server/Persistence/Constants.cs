namespace Server.Persistence
{
    public static class Constants
    {
        /// <summary>
        /// The path storing the persistent data
        /// </summary>
        public static readonly string DataPath = "data";

        /// <summary>
        /// Before a drop or a full import a copy of all persistent data is stored here to allow for a last chance recovery
        /// </summary>
        public static readonly string RollbackDataPath = "_data";
        
        
        public static readonly string SchemaFileName = "schema.json";
        
        public static readonly string SequenceFileName = "sequence.json";
        
        public static readonly string NodeConfigFileName = "node_config.json";

        
        /// <summary>
        /// Threshold to trigger the bik insert optimization for a PutMany operation
        /// </summary>
        public static readonly int BulkThreshold = 50;

        /// <summary>
        /// Delay before commiting into the storage a transaction from the transaction log. This allows the transaction to be rolled back after being written
        /// in the transaction log. Used in two-stage transactions
        /// </summary>
        public static readonly int DelayForTwoStageTransactionsInMilliseconds = 2000;
        
        /// <summary>
        /// Delay to acquire lock for a two stage transaction. It is the delay for one iteration of the nodes
        /// </summary>
        public static readonly int DelayForLockInMilliseconds = 20;
    }
}