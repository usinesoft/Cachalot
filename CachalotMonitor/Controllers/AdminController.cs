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

        [HttpPost("connect")]
        public ConnectionResponse Connect([FromBody] ConnectionInfo connectionInfo)
        {
            try
            {
                var connectionString = ClusterService.Connect(connectionInfo);



                return new ConnectionResponse { ConnectionString = connectionString };

            }
            catch (Exception ex) 
            {
                return new ConnectionResponse { ErrorMessage = ex.Message };
            }


        }

        [HttpGet]
        public ClusterInformation GetClusterInformation()
        {
            
            return ClusterService.GetClusterInformation();

        }
    }
}
