namespace ProjectAPI.Domain.Identity.Entities;

/// <summary>Default action permission inherited by every user holding a role.</summary>
public class RolePermission
{
    public Guid Id { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public bool Allowed { get; set; } = true;
}

/// <summary>One real HTTP action exposed by the API.  The catalogue is synchronised
/// from MVC action descriptors; it is not a hand-maintained CRUD checklist.</summary>
public class PermissionEndpoint
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Controller { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string RouteTemplate { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>Per-user allow/deny override. It always wins over the role default.</summary>
public class UserPermissionOverride
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public bool Allowed { get; set; }
}
