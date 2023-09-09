using System.Collections.Generic;
using System.Linq;
using System.Text;
using Client.Core;
using Client.Messages;

namespace Client.Interface;

/// <summary>
///     Synthetic view of the cluster
///     Contains:
///     - Global status of the cluster
///     - Status of individual servers
///     - If at least a server is connected will also contain collections data: summary and detailed schema
/// </summary>
public class ClusterInformation
{
    public enum ClusterStatus
    {
        /// <summary>
        ///     All good. All servers responded and cluster consistent
        /// </summary>
        Ok,

        /// <summary>
        ///     Non blocking consistency checks failed
        /// </summary>
        Warning,

        /// <summary>
        ///     Blocking consistency checks failed
        /// </summary>
        InconsistentCluster,

        /// <summary>
        ///     A connection was attempted but at least one server did not respond
        /// </summary>
        ConnectionError,

        /// <summary>
        ///     No connection was attempted
        /// </summary>
        NotConnected
    }

    /// <summary>
    ///     Build from a list of <see cref="ServerDescriptionResponse" /> which may contain servers that did not respond to
    ///     connection request
    /// </summary>
    /// <param name="serverDescriptions"></param>
    public ClusterInformation(ServerDescriptionResponse[] serverDescriptions)
    {
        // by convention an empty array is sent if no connection attempt has been done
        if (!serverDescriptions.Any())
        {
            Status = ClusterStatus.NotConnected;
            StatusReason = "Not connected";

            return;
        }

        ServersStatus = serverDescriptions.Select(d => d.ServerProcessInfo).ToArray();

        if (serverDescriptions.All(s => s.ConnectionError))
        {
            Status = ClusterStatus.ConnectionError;
            StatusReason = "Connection error";

            return;
        }


        // at this point there is at least one connected server
        Schema = serverDescriptions.FirstOrDefault(x => !x.ConnectionError)?.KnownTypesByFullName.Values.ToArray();


        ////////////////////////////////////////////////////////////////////////////////////////
        // Collection information is a little bit messy: split between Schema and DataStoreInfo

        Dictionary<string, CollectionSummary> collectionSummaries = new();

        // aggregate information for collections on each server
        foreach (var description in serverDescriptions.Where(x => !x.ConnectionError))
        foreach (var dataStoreInfo in description.DataStoreInfoByFullName)
        {
            var name = dataStoreInfo.Key;
            if (!collectionSummaries.TryGetValue(name, out var info))
            {
                info = new();
                collectionSummaries[name] = info;
                info.Name = name;
            }

            info.ItemsCount += dataStoreInfo.Value.Count;

            info.EvictionType = dataStoreInfo.Value.EvictionPolicy;
        }

        // get the layout and full-text indexation information from the schema
        if (Schema != null)
            foreach (var collectionSchema in Schema)
            {
                collectionSummaries[collectionSchema.CollectionName].FullTextSearch =
                    collectionSchema.FullText?.Count > 0;

                collectionSummaries[collectionSchema.CollectionName].StorageLayout = collectionSchema.StorageLayout;
            }


        CollectionsSummary = collectionSummaries.Values.ToArray();

        var allGood = serverDescriptions.All(r => !r.ConnectionError);

        // If all servers are connected perform a sanity check for cluster consistency
        if (allGood)
        {
            CheckClusterSanity(serverDescriptions);

            var cxString = new StringBuilder();
            foreach (var serverDescription in serverDescriptions)
            {
                cxString.Append(serverDescription.ServerProcessInfo.Host);
                cxString.Append(":");
                cxString.Append(serverDescription.ServerProcessInfo.Port);
                cxString.Append("+");
            }

            ConnectionString = cxString.ToString().TrimEnd('+');
        }
        else
        {
            Status = ClusterStatus.ConnectionError;
            StatusReason = "At least one server did not respond";
        }
    }

    public string ConnectionString { get; }

    public ServerInfo[] ServersStatus { get; }
    public CollectionSchema[] Schema { get; }

    public CollectionSummary[] CollectionsSummary { get; }

    public ClusterStatus Status { get; private set; }

    public string StatusReason { get; private set; }

    /// <summary>
    ///     Check if the nodes in the cluster are consistent: same persistent/non persistent otherwise KO
    ///     If clusterName or memoryLimit are different => Warning: Can work but probably configuration error
    /// </summary>
    /// <param name="responses"></param>
    private void CheckClusterSanity(ServerDescriptionResponse[] responses)
    {
        if (Status == ClusterStatus.NotConnected)
            return;

        if (ServersStatus.Length > 0)
        {
            // check that all schemas are identical

            var reference = responses[0];
            for (var i = 1; i < responses.Length; i++)
                foreach (var typeDescription in reference.KnownTypesByFullName)
                {
                    if (!responses[i].KnownTypesByFullName.ContainsKey(typeDescription.Key))
                    {
                        Status = ClusterStatus.InconsistentCluster;
                        StatusReason =
                            "Servers have different schemas (collection not defined). You are probably trying to connect to servers that belong to different clusters";

                        break;
                    }

                    if (!responses[i].KnownTypesByFullName[typeDescription.Key].Equals(typeDescription.Value))
                    {
                        Status = ClusterStatus.InconsistentCluster;
                        StatusReason =
                            "Servers have different schemas (collection schemas are different). You are probably trying to connect to servers that belong to different clusters";

                        break;
                    }
                }


            if (Status == ClusterStatus.InconsistentCluster)
                return;


            var memoryLimit = ServersStatus[0].MemoryLimitInGigabytes;
            var clusterName = ServersStatus[0].ClusterName;
            var isPersistent = ServersStatus[0].IsPersistent;

            for (var i = 1; i < ServersStatus.Length; i++)
                if (ServersStatus[i].IsPersistent != isPersistent)
                {
                    Status = ClusterStatus.InconsistentCluster;
                    StatusReason = "Mixing persistent and non persistent nodes. This can not work";
                    break;
                }

            if (Status == ClusterStatus.Ok)
                for (var i = 1; i < ServersStatus.Length; i++)
                {
                    if (ServersStatus[i].ClusterName != clusterName)
                    {
                        Status = ClusterStatus.Warning;
                        StatusReason =
                            "The cluster name is not the same for all nodes. Probably configuration issue";
                        break;
                    }

                    if (ServersStatus[i].MemoryLimitInGigabytes != memoryLimit)
                    {
                        Status = ClusterStatus.Warning;
                        StatusReason =
                            "The memory limit is not the same for all nodes. The cluster will work but the memory use will not be optimal";
                        break;
                    }
                }
        }
    }
}

public class CollectionSummary
{
    public string Name { get; set; }

    public long ItemsCount { get; set; }

    public Layout StorageLayout { get; set; }

    public EvictionType EvictionType { get; set; }

    public bool FullTextSearch { get; set; }
}