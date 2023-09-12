using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Distributed;

namespace CachalotMonitor.Services;


/// <summary>
/// Action filter for admin authorization
/// </summary>
public class AuthenticationFilter : ActionFilterAttribute
{
    
    public AuthenticationFilter()
    {
    
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var token = context.HttpContext.Request.Headers["x-token"];
        if (token.Count == 0)
        {
            context.Result = new UnauthorizedResult();
            return;
        }
            

        var auth = context.HttpContext.RequestServices.GetService<IAuthenticationService>();
        if (auth == null) throw new NotSupportedException("Authentication service not registered");

        var code = token[0];

        if (auth.CheckAdminCode(code))
        {
            base.OnActionExecuting(context);
        }
        else
        {
            context.Result = new UnauthorizedResult();
        }

        
    }


}