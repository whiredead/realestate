using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Identity.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Controllers;
[ApiController, Route("api/permissions"), Authorize(Roles = RoleGroups.Admins)]
public sealed class PermissionsController : ControllerBase
{
 private readonly ApplicationDbContext _db; private readonly IActionDescriptorCollectionProvider _actions; private readonly PermissionCacheVersion _cacheVersion;
 public PermissionsController(ApplicationDbContext db, IActionDescriptorCollectionProvider actions, PermissionCacheVersion cacheVersion) => (_db,_actions,_cacheVersion)=(db,actions,cacheVersion);
 [HttpGet("catalog")] public async Task<IActionResult> Catalog(CancellationToken ct)=>Ok(await _db.PermissionEndpoints.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.Module).ThenBy(x=>x.Controller).ThenBy(x=>x.RouteTemplate).ToListAsync(ct));
 [HttpPost("catalog/sync")] public async Task<IActionResult> SyncCatalog(CancellationToken ct) { var found=new List<PermissionEndpoint>(); foreach(var action in _actions.ActionDescriptors.Items.OfType<ControllerActionDescriptor>()) { if(action.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any()||action.MethodInfo.GetCustomAttributes(typeof(AllowAnonymousAttribute),true).Any())continue; var methods=action.ActionConstraints?.OfType<HttpMethodActionConstraint>().SelectMany(x=>x.HttpMethods).DefaultIfEmpty("ANY")??["ANY"]; foreach(var method in methods) { var controller=action.ControllerName; var route=action.AttributeRouteInfo?.Template??$"api/{controller}/{action.ActionName}"; found.Add(new PermissionEndpoint{Key=$"{controller}.{action.ActionName}.{method}".ToLowerInvariant(),Module=ModuleFor(controller),Controller=controller,ActionName=action.ActionName,HttpMethod=method.ToUpperInvariant(),RouteTemplate=route,IsActive=true}); } } var existing=await _db.PermissionEndpoints.ToDictionaryAsync(x=>x.Key,ct); foreach(var endpoint in found) { if(existing.TryGetValue(endpoint.Key,out var row)){row.Module=endpoint.Module;row.Controller=endpoint.Controller;row.ActionName=endpoint.ActionName;row.HttpMethod=endpoint.HttpMethod;row.RouteTemplate=endpoint.RouteTemplate;row.IsActive=true;} else _db.PermissionEndpoints.Add(endpoint); } var keys=found.Select(x=>x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase); foreach(var row in existing.Values.Where(x=>!keys.Contains(x.Key)))row.IsActive=false; await _db.SaveChangesAsync(ct);return Ok(new { count=found.Count }); }
 [HttpGet("users/{userId}")] public async Task<IActionResult> GetUser(string userId,CancellationToken ct) => Ok(await _db.UserPermissionOverrides.AsNoTracking().Where(x=>x.UserId==userId).ToListAsync(ct));
 [HttpDelete("users/{userId}")] public async Task<IActionResult> ClearUser(string userId,[FromQuery] string resource,[FromQuery] string action,CancellationToken ct) { var row=await _db.UserPermissionOverrides.FirstOrDefaultAsync(x=>x.UserId==userId&&x.Resource==resource&&x.Action==action,ct); if(row is not null){_db.Remove(row);await _db.SaveChangesAsync(ct);_cacheVersion.Advance();} return NoContent(); }
 [HttpPut("users/{userId}")] public async Task<IActionResult> SetUser(string userId,[FromBody] PermissionInput input,CancellationToken ct) { var row=await _db.UserPermissionOverrides.FirstOrDefaultAsync(x=>x.UserId==userId&&x.Resource==input.Resource&&x.Action==input.Action,ct); if(row is null){row=new UserPermissionOverride{Id=Guid.NewGuid(),UserId=userId,Resource=input.Resource,Action=input.Action};_db.Add(row);}row.Allowed=input.Allowed;await _db.SaveChangesAsync(ct);_cacheVersion.Advance();return Ok(row); }
 [HttpGet("roles/{roleCode}")] public async Task<IActionResult> GetRole(string roleCode,CancellationToken ct)=>Ok(await _db.RolePermissions.AsNoTracking().Where(x=>x.RoleCode==roleCode).ToListAsync(ct));
 [HttpPost("role-baseline")] public IActionResult RoleBaseline([FromBody] RoleBaselineRequest input)
 {
     var roles = input.RoleCodes.Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
     var result = new List<RoleBaselinePermission>();
     foreach (var action in _actions.ActionDescriptors.Items.OfType<ControllerActionDescriptor>())
     {
         if (action.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any() || action.MethodInfo.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Any()) continue;
         var authorizations = action.EndpointMetadata.OfType<IAuthorizeData>()
             .Concat(action.MethodInfo.GetCustomAttributes(typeof(IAuthorizeData), true).Cast<IAuthorizeData>())
             .Concat(action.ControllerTypeInfo.GetCustomAttributes(typeof(IAuthorizeData), true).Cast<IAuthorizeData>())
             .Where(x => !string.IsNullOrWhiteSpace(x.Roles))
             .Select(x => x.Roles!)
             .Distinct(StringComparer.OrdinalIgnoreCase)
             .ToArray();
         if (authorizations.Length == 0) continue;
         var allowed = authorizations.All(csv => csv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Any(roles.Contains));
         var methods = action.ActionConstraints?.OfType<HttpMethodActionConstraint>().SelectMany(x => x.HttpMethods).DefaultIfEmpty("ANY") ?? ["ANY"];
         foreach (var method in methods) result.Add(new RoleBaselinePermission($"{action.ControllerName}.{action.ActionName}.{method}".ToLowerInvariant(), allowed));
     }
     return Ok(result);
 }
 [HttpPut("roles/{roleCode}")] public async Task<IActionResult> SetRole(string roleCode,[FromBody] PermissionInput input,CancellationToken ct) { var row=await _db.RolePermissions.FirstOrDefaultAsync(x=>x.RoleCode==roleCode&&x.Resource==input.Resource&&x.Action==input.Action,ct);if(row is null){row=new RolePermission{Id=Guid.NewGuid(),RoleCode=roleCode,Resource=input.Resource,Action=input.Action};_db.Add(row);}row.Allowed=input.Allowed;await _db.SaveChangesAsync(ct);_cacheVersion.Advance();return Ok(row); }
 private static string ModuleFor(string controller)=>controller switch { var c when c.Contains("Project",StringComparison.OrdinalIgnoreCase)||c.Contains("Immeuble",StringComparison.OrdinalIgnoreCase)||c.Contains("TypeBien",StringComparison.OrdinalIgnoreCase)=>"Patrimoine", var c when c.Contains("Reservation",StringComparison.OrdinalIgnoreCase)||c.Contains("Sale",StringComparison.OrdinalIgnoreCase)||c.Contains("Appointment",StringComparison.OrdinalIgnoreCase)=>"Commercialisation", var c when c.Contains("Notary",StringComparison.OrdinalIgnoreCase)=>"Notaire", var c when c.Contains("Claim",StringComparison.OrdinalIgnoreCase)||c.Contains("Feedback",StringComparison.OrdinalIgnoreCase)=>"Après-vente", _=>"Administration"};
}
public sealed record PermissionInput(string Resource,string Action,bool Allowed);
public sealed record RoleBaselineRequest(IReadOnlyCollection<string> RoleCodes);
public sealed record RoleBaselinePermission(string Resource,bool Allowed);
