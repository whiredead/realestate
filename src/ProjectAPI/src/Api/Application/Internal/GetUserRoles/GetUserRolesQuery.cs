namespace ProjectAPI.Api.Application.Internal.GetUserRoles;

/// <summary>
/// Phase 2 — lets ProjectAPI's membership-grant handlers verify a target
/// account's real platform roles, rather than checking ProjectAPI's own
/// separate (and never-synced) copy of AspNetUserRoles.
/// </summary>
public class GetUserRolesQuery : IRequest<GetUserRolesResponse>
{
    public required string UserId { get; init; }
}
