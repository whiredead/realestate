
using ProjectAPI.Api.Application.Common.Audit;
using ProjectAPI.Api.Application.Common.Models;

namespace ProjectAPI.Api.Application.Identity.Users.Login;

/// <summary>
/// Represents the response model for user login operations.
/// </summary>
public record LoginResponse : IAuditOutcome
{
    /// <summary>
    /// Gets or sets the access token for the authenticated user.
    /// </summary>
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }

    /// <summary>
    /// Gets or sets a boolean indicating whether the user is authenticated.
    /// </summary>
    public bool IsAutheticated { get; init; }

    /// <summary>
    /// Gets or sets a message describing the result of the login operation.
    /// </summary>
    public string Message { get; init; } = default!;

    /// <summary>
    /// Gets or sets the user information in the response.
    /// </summary>
    public UserResponse? User { get; init; }

    /// <summary>
    /// Gets or sets the role of the user.
    /// </summary>
    public IList<string>? Roles { get; set; } = [];

    // A refused login is a 200 with IsAutheticated = false, not an exception: tell the
    // audit trail so it is journaled as a failure. Explicit members, so not serialised.
    bool IAuditOutcome.Succeeded => IsAutheticated;
    string? IAuditOutcome.FailureReason => IsAutheticated ? null : Message;
    string? IAuditOutcome.ActorUserId => IsAutheticated ? User?.Id : null;
    string? IAuditOutcome.ActorRoles => IsAutheticated && Roles is { Count: > 0 } ? string.Join(",", Roles) : null;
}
