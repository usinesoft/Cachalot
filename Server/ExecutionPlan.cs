namespace Server
{
    /// <summary>
    ///     Summary of the strategy used to resolve a query
    /// </summary>
    public class ExecutionPlan
    {
        public string PrimaryIndexName { get; set; }

        public int ElementsInPrimarySet { get; set; }

        public bool IsFullScan => PrimaryIndexName == null;

        public override string ToString()
        {
            if (IsFullScan)
                return "full scan";

            return PrimaryIndexName + ":" + ElementsInPrimarySet;
        }
    }
}