using CachalotMonitor.Model;
using CachalotMonitor.Services;
using Client.Interface;
using Microsoft.AspNetCore.Mvc;
using ConnectionInfo = CachalotMonitor.Model.ConnectionInfo;

namespace CachalotMonitor.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {

        IClusterService ClusterService { get; }

        public AdminController(IClusterService clusterService)
        {
            ClusterService = clusterService;
        }

        /// <summary>
        /// Connect with explicit connection data
        /// </summary>
        /// <param name="connectionInfo"></param>
        /// <returns></returns>
        [HttpPost("connect")]
        public ConnectionResponse Connect([FromBody] ConnectionInfo connectionInfo)
        {
            try
            {
                var connectionString = ClusterService.Connect(connectionInfo);

                var clusterInfo  = GetClusterInformation();

                if (clusterInfo.Status == ClusterInformation.ClusterStatus.Ok)
                {
                    ClusterService.SaveToConnectionHistory(connectionInfo, clusterInfo.ServersStatus[0].ClusterName);
                }

                
                return new ConnectionResponse { ConnectionString = connectionString };

            }
            catch (Exception ex) 
            {
                return new ConnectionResponse { ErrorMessage = ex.Message };
            }


        }


        /// <summary>
        /// Connect to a cluster from the connection history
        /// </summary>
        /// <param name="clusterName"></param>
        /// <returns></returns>
        [HttpPost("connect/{clusterName}")]
        public ConnectionResponse ConnectWithHistory(string clusterName)
        {
            try
            {
                var info = ClusterService.GetFromConnectionHistory(clusterName);

                var connectionString = ClusterService.Connect(info);

                // works like a high level ping
                var _  = GetClusterInformation();
                
                return new ConnectionResponse { ConnectionString = connectionString };

            }
            catch (Exception ex) 
            {
                return new ConnectionResponse { ErrorMessage = ex.Message };
            }
            
        }


        /// <summary>
        /// Disconnect from current cluster
        /// </summary>
        /// <returns></returns>
        [HttpPost("disconnect")]
        public void Disconnect()
        {
            ClusterService.Disconnect();
        }

        /// <summary>
        /// Get history of successful connections
        /// </summary>
        /// <returns></returns>
        [HttpGet("history")]
        public string[] GetConnectionHistory()
        {
            return ClusterService.GetHistoryEntries();
        }


        /// <summary>
        /// Get information about cluster nodes and collections
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public ClusterInformation GetClusterInformation()
        {
            return ClusterService.GetClusterInformation();

        }
    }
}
