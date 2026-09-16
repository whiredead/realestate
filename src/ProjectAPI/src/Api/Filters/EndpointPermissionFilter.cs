using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using ProjectAPI.Api.Application.Common.Security;

namespace ProjectAPI.Api.Filters;

/// <summary>Applies the configured, endpoint-level override after normal
/// authentication/role checks.  Until an endpoint is managed by a role or an
/// override it retains the existing controller authorization behaviour.</summary>
public sealed class EndpointPermissionFilter : IAsyncActionFilter
{
    private readonly IPermissionService _permissions;
    public EndpointPermissionFilter(IPermissionService permissions) => _permissions = permissions;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor action || IsAnonymous(action)) { await next(); return; }
        var resource = $"{action.ControllerName}.{action.ActionName}.{context.HttpContext.Request.Method}".ToLowerInvariant();
        if (await _permissions.IsManagedAsync(resource, "EXECUTE", context.HttpContext.RequestAborted)
            && !await _permissions.CanAsync(resource, "EXECUTE", context.HttpContext.RequestAborted))
        {
            context.Result = new ForbidResult();
            return;
        }
        await next();
    }

    private static bool IsAnonymous(ControllerActionDescriptor action)
        => action.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any()
           || action.MethodInfo.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Any()
           || action.ControllerTypeInfo.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Any();
}
