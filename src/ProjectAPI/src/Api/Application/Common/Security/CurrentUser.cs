using System.Security.Claims;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Api.Application.Common.Security;

/// <summary>
/// The authenticated caller, expressed in spec §6.1 role codes.
///
/// Handlers MUST resolve identity through this abstraction rather than reading
/// claims ad hoc: §6.4 requires every project-scoped request to check the
/// caller's assignment, and that check is only auditable if there is exactly
/// one place that answers "who is this and what may they see".
/// </summary>
public interface ICurrentUser
{
    /// <summary>AspNetUsers.Id of the caller, or null when unauthenticated.</summary>
    string? UserId { get; }

    /// <summary>True when a bearer token was presented and accepted.</summary>
    bool IsAuthenticated { get; }

    /// <summary>Caller roles, normalised to §6.1 codes.</summary>
    IReadOnlyCollection<string> Roles { get; }

    /// <summary>True when the caller holds the given §6.1 role code.</summary>
    bool IsInRole(string roleCode);

    /// <summary>
    /// True when the caller sees the whole platform (§6.3 "Admin global").
    /// A GLOBAL_ADMIN bypasses per-project scope checks — no other role does.
    /// </summary>
    bool IsGlobalAdmin { get; }
}

/// <inheritdoc />
public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;
    private string[]? _roles;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public string? UserId =>
        Principal?.FindFirst("userId")?.Value
        ?? Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public IReadOnlyCollection<string> Roles =>
        _roles ??= RoleCodes.Normalize(
            Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? Enumerable.Empty<string>());

    public bool IsInRole(string roleCode) => Roles.Contains(roleCode, StringComparer.Ordinal);

    public bool IsGlobalAdmin => IsInRole(RoleCodes.GlobalAdmin);
}
