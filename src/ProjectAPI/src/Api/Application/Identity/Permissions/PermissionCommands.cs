using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Identity.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Identity.Permissions;

public sealed record PermissionInput(string Resource, string Action, bool Allowed);
public sealed record RoleBaselineRequest(IReadOnlyCollection<string> RoleCodes);
public sealed record RoleBaselinePermission(string Resource, bool Allowed);
public sealed record SyncCatalogResult(int Count);

/// <summary>Reads the API's own endpoints: the permission catalogue is keyed "controller.action.method".</summary>
internal static class EndpointCatalog
{
    public static bool IsAnonymous(ControllerActionDescriptor action) =>
        action.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any() || action.MethodInfo.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Any();

    public static IEnumerable<string> Methods(ControllerActionDescriptor action) =>
        action.ActionConstraints?.OfType<HttpMethodActionConstraint>().SelectMany(x => x.HttpMethods).DefaultIfEmpty("ANY") ?? ["ANY"];

    public static string ModuleFor(string controller) => controller switch
    {
        var c when c.Contains("Project", StringComparison.OrdinalIgnoreCase) || c.Contains("Immeuble", StringComparison.OrdinalIgnoreCase) || c.Contains("TypeBien", StringComparison.OrdinalIgnoreCase) => "Patrimoine",
        var c when c.Contains("Reservation", StringComparison.OrdinalIgnoreCase) || c.Contains("Sale", StringComparison.OrdinalIgnoreCase) || c.Contains("Appointment", StringComparison.OrdinalIgnoreCase) => "Commercialisation",
        var c when c.Contains("Notary", StringComparison.OrdinalIgnoreCase) => "Notaire",
        var c when c.Contains("Claim", StringComparison.OrdinalIgnoreCase) || c.Contains("Feedback", StringComparison.OrdinalIgnoreCase) => "Après-vente",
        _ => "Administration"
    };
}

// GET /api/permissions/catalog
public class GetPermissionCatalogQuery : IRequest<List<PermissionEndpoint>> { }

public class GetPermissionCatalogHandler : IRequestHandler<GetPermissionCatalogQuery, List<PermissionEndpoint>>
{
    private readonly ApplicationDbContext _db;
    public GetPermissionCatalogHandler(ApplicationDbContext db) => _db = db;

    public Task<List<PermissionEndpoint>> Handle(GetPermissionCatalogQuery request, CancellationToken ct) =>
        _db.PermissionEndpoints.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.Module).ThenBy(x => x.Controller).ThenBy(x => x.RouteTemplate)
            .ToListAsync(ct);
}

// POST /api/permissions/catalog/sync — upserts every authenticated endpoint, deactivates the ones that disappeared.
public class SyncPermissionCatalogCommand : IRequest<SyncCatalogResult> { }

public class SyncPermissionCatalogHandler : IRequestHandler<SyncPermissionCatalogCommand, SyncCatalogResult>
{
    private readonly ApplicationDbContext _db;
    private readonly IActionDescriptorCollectionProvider _actions;
    public SyncPermissionCatalogHandler(ApplicationDbContext db, IActionDescriptorCollectionProvider actions) { _db = db; _actions = actions; }

    public async Task<SyncCatalogResult> Handle(SyncPermissionCatalogCommand request, CancellationToken ct)
    {
        var found = new List<PermissionEndpoint>();
        foreach (var action in _actions.ActionDescriptors.Items.OfType<ControllerActionDescriptor>())
        {
            if (EndpointCatalog.IsAnonymous(action)) continue;
            foreach (var method in EndpointCatalog.Methods(action))
            {
                var controller = action.ControllerName;
                var route = action.AttributeRouteInfo?.Template ?? $"api/{controller}/{action.ActionName}";
                found.Add(new PermissionEndpoint
                {
                    Key = $"{controller}.{action.ActionName}.{method}".ToLowerInvariant(),
                    Module = EndpointCatalog.ModuleFor(controller),
                    Controller = controller,
                    ActionName = action.ActionName,
                    HttpMethod = method.ToUpperInvariant(),
                    RouteTemplate = route,
                    IsActive = true
                });
            }
        }

        var existing = await _db.PermissionEndpoints.ToDictionaryAsync(x => x.Key, ct);
        foreach (var endpoint in found)
        {
            if (existing.TryGetValue(endpoint.Key, out var row))
            {
                row.Module = endpoint.Module;
                row.Controller = endpoint.Controller;
                row.ActionName = endpoint.ActionName;
                row.HttpMethod = endpoint.HttpMethod;
                row.RouteTemplate = endpoint.RouteTemplate;
                row.IsActive = true;
            }
            else _db.PermissionEndpoints.Add(endpoint);
        }
        var keys = found.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in existing.Values.Where(x => !keys.Contains(x.Key))) row.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return new SyncCatalogResult(found.Count);
    }
}

// GET /api/permissions/users/{userId}
public class GetUserPermissionsQuery : IRequest<List<UserPermissionOverride>>
{
    public string UserId { get; set; } = string.Empty;
}

public class GetUserPermissionsHandler : IRequestHandler<GetUserPermissionsQuery, List<UserPermissionOverride>>
{
    private readonly ApplicationDbContext _db;
    public GetUserPermissionsHandler(ApplicationDbContext db) => _db = db;

    public Task<List<UserPermissionOverride>> Handle(GetUserPermissionsQuery request, CancellationToken ct) =>
        _db.UserPermissionOverrides.AsNoTracking().Where(x => x.UserId == request.UserId).ToListAsync(ct);
}

// PUT /api/permissions/users/{userId}
public class SetUserPermissionCommand : IRequest<UserPermissionOverride>
{
    public string UserId { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public bool Allowed { get; set; }
}

public class SetUserPermissionHandler : IRequestHandler<SetUserPermissionCommand, UserPermissionOverride>
{
    private readonly ApplicationDbContext _db;
    private readonly PermissionCacheVersion _cacheVersion;
    public SetUserPermissionHandler(ApplicationDbContext db, PermissionCacheVersion cacheVersion) { _db = db; _cacheVersion = cacheVersion; }

    public async Task<UserPermissionOverride> Handle(SetUserPermissionCommand request, CancellationToken ct)
    {
        var row = await _db.UserPermissionOverrides.FirstOrDefaultAsync(x => x.UserId == request.UserId && x.Resource == request.Resource && x.Action == request.Action, ct);
        if (row is null)
        {
            row = new UserPermissionOverride { Id = Guid.NewGuid(), UserId = request.UserId, Resource = request.Resource, Action = request.Action };
            _db.Add(row);
        }
        row.Allowed = request.Allowed;
        await _db.SaveChangesAsync(ct);
        _cacheVersion.Advance();
        return row;
    }
}

// DELETE /api/permissions/users/{userId}?resource=&action= — the user falls back to their roles' rules.
public class ClearUserPermissionCommand : IRequest<MediatR.Unit>
{
    public string UserId { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}

public class ClearUserPermissionHandler : IRequestHandler<ClearUserPermissionCommand, MediatR.Unit>
{
    private readonly ApplicationDbContext _db;
    private readonly PermissionCacheVersion _cacheVersion;
    public ClearUserPermissionHandler(ApplicationDbContext db, PermissionCacheVersion cacheVersion) { _db = db; _cacheVersion = cacheVersion; }

    public async Task<MediatR.Unit> Handle(ClearUserPermissionCommand request, CancellationToken ct)
    {
        var row = await _db.UserPermissionOverrides.FirstOrDefaultAsync(x => x.UserId == request.UserId && x.Resource == request.Resource && x.Action == request.Action, ct);
        if (row is not null)
        {
            _db.Remove(row);
            await _db.SaveChangesAsync(ct);
            _cacheVersion.Advance();
        }
        return MediatR.Unit.Value;
    }
}

// GET /api/permissions/roles/{roleCode}
public class GetRolePermissionsQuery : IRequest<List<RolePermission>>
{
    public string RoleCode { get; set; } = string.Empty;
}

public class GetRolePermissionsHandler : IRequestHandler<GetRolePermissionsQuery, List<RolePermission>>
{
    private readonly ApplicationDbContext _db;
    public GetRolePermissionsHandler(ApplicationDbContext db) => _db = db;

    public Task<List<RolePermission>> Handle(GetRolePermissionsQuery request, CancellationToken ct) =>
        _db.RolePermissions.AsNoTracking().Where(x => x.RoleCode == request.RoleCode).ToListAsync(ct);
}

// PUT /api/permissions/roles/{roleCode}
public class SetRolePermissionCommand : IRequest<RolePermission>
{
    public string RoleCode { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public bool Allowed { get; set; }
}

public class SetRolePermissionHandler : IRequestHandler<SetRolePermissionCommand, RolePermission>
{
    private readonly ApplicationDbContext _db;
    private readonly PermissionCacheVersion _cacheVersion;
    public SetRolePermissionHandler(ApplicationDbContext db, PermissionCacheVersion cacheVersion) { _db = db; _cacheVersion = cacheVersion; }

    public async Task<RolePermission> Handle(SetRolePermissionCommand request, CancellationToken ct)
    {
        var row = await _db.RolePermissions.FirstOrDefaultAsync(x => x.RoleCode == request.RoleCode && x.Resource == request.Resource && x.Action == request.Action, ct);
        if (row is null)
        {
            row = new RolePermission { Id = Guid.NewGuid(), RoleCode = request.RoleCode, Resource = request.Resource, Action = request.Action };
            _db.Add(row);
        }
        row.Allowed = request.Allowed;
        await _db.SaveChangesAsync(ct);
        _cacheVersion.Advance();
        return row;
    }
}

// POST /api/permissions/role-baseline — what the [Authorize(Roles)] attributes alone grant to the given roles.
public class GetRoleBaselineQuery : IRequest<List<RoleBaselinePermission>>
{
    public IReadOnlyCollection<string> RoleCodes { get; set; } = [];
}

public class GetRoleBaselineHandler : IRequestHandler<GetRoleBaselineQuery, List<RoleBaselinePermission>>
{
    private readonly IActionDescriptorCollectionProvider _actions;
    public GetRoleBaselineHandler(IActionDescriptorCollectionProvider actions) => _actions = actions;

    public Task<List<RoleBaselinePermission>> Handle(GetRoleBaselineQuery request, CancellationToken ct)
    {
        var roles = request.RoleCodes.Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new List<RoleBaselinePermission>();
        foreach (var action in _actions.ActionDescriptors.Items.OfType<ControllerActionDescriptor>())
        {
            if (EndpointCatalog.IsAnonymous(action)) continue;
            var authorizations = action.EndpointMetadata.OfType<IAuthorizeData>()
                .Concat(action.MethodInfo.GetCustomAttributes(typeof(IAuthorizeData), true).Cast<IAuthorizeData>())
                .Concat(action.ControllerTypeInfo.GetCustomAttributes(typeof(IAuthorizeData), true).Cast<IAuthorizeData>())
                .Where(x => !string.IsNullOrWhiteSpace(x.Roles))
                .Select(x => x.Roles!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (authorizations.Length == 0) continue;
            var allowed = authorizations.All(csv => csv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Any(roles.Contains));
            foreach (var method in EndpointCatalog.Methods(action))
                result.Add(new RoleBaselinePermission($"{action.ControllerName}.{action.ActionName}.{method}".ToLowerInvariant(), allowed));
        }
        return Task.FromResult(result);
    }
}
