using Client.Core;

namespace Server
{
    public interface ILog
    {
        void LogActivity(string type, string collectionName, int executionTimeInMicroseconds, string detail, string query = null,
            ExecutionPlan plan = null);

        /// <summary>
        /// A non persistent table that can be queried like a normal table storing the server activity
        /// </summary>
        DataStore ActivityTable { get; }


        void LogDebug(string message);
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message);
    }
}