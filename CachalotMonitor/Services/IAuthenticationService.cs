using System.Runtime.Intrinsics.Arm;

namespace CachalotMonitor.Services;

public interface IAuthenticationService
{
    bool CheckAdminCode(string adminCode);
}