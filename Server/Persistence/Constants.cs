namespace Server.Persistence
{
    public static class Constants
    {
        public static readonly string DataPath = "data";
        public static readonly string RollbackDataPath = "_data";
        public static readonly string SchemaFileName = "schema.json";
        public static readonly string SequenceFileName = "sequence.json";
        public static readonly string NodeConfigFileName = "node_config.json";

        public static readonly int DefaultPort = 4848;

        //TODO need do be configurable
        public static readonly int BulkThreshold = 50;

        public static readonly int DelayForTwoStageTransactionsInMilliseconds = 2000;
        public static readonly int DelayForLock = 20;
    }
}