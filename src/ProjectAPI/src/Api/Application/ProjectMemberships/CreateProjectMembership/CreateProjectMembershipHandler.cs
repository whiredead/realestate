using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.ProjectMemberships.CreateProjectMembership;

/// <summary>
/// Grants a project membership. A PROJECT_ADMIN may only grant it inside
/// their own perimeter (checked via ProjectScopeService, same as every other
/// project-scoped handler); only GLOBAL_ADMIN is unrestricted.
///
/// The requested RoleCode must match a role the target user actually holds
/// on their account (AspNetUserRoles) — a membership is a PROJECT-scoped
/// grant of a role the platform has already assigned to the account, never a
/// way to grant a role that account doesn't have. Without this check an
/// admin could hand out NOTARY project access to a plain SALES_AGENT account
/// simply by creating the right membership row, which would let that
/// account reach notary-only endpoints those endpoints gate purely on
/// [Authorize(Roles=NOTARY)] plus this membership.
///
/// If an ACTIVE membership already exists for this exact
/// (UserId, ProjectId, RoleCode), it is deactivated in the SAME transaction
/// as the new insert — this is what keeps the filtered unique index
/// (IX_ProjectMemberships_ActivePerUserProjectRole) from ever being violated
/// by application logic; the index itself is only the last-resort,
/// race-proof backstop (see ProjectMembershipConfiguration).
/// </summary>
public class CreateProjectMembershipHandler : IRequestHandler<CreateProjectMembershipCommand, CreateProjectMembershipResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<User> _userManager;
    private readonly ProjectScopeService _projectScope;
    private readonly ICurrentUser _currentUser;

    public CreateProjectMembershipHandler(
        ApplicationDbContext db, UserManager<User> userManager, ProjectScopeService projectScope, ICurrentUser currentUser)
    {
        _db = db;
        _userManager = userManager;
        _projectScope = projectScope;
        _currentUser = currentUser;
    }

    public async Task<CreateProjectMembershipResponse> Handle(CreateProjectMembershipCommand request, CancellationToken cancellationToken)
    {
        await _projectScope.EnsureProjectAccessAsync(request.ProjectId, cancellationToken);

        var roleCode = RoleCodes.Normalize(request.RoleCode);
        if (!RoleCodes.MembershipRoles.Contains(roleCode, StringComparer.Ordinal))
        {
            throw new Common.Exceptions.ValidationException(new[]
            {
                new ValidationFailure(nameof(request.RoleCode),
                    $"RoleCode must be one of: {string.Join(", ", RoleCodes.MembershipRoles)}.")
            });
        }

        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            throw new Common.Exceptions.ValidationException(new[]
            {
                new ValidationFailure(nameof(request.UserId), "UserId is required.")
            });
        }

        var targetUser = await _userManager.FindByIdAsync(request.UserId);
        if (targetUser == null)
        {
            throw new NotFoundException($"User {request.UserId} not found.");
        }

        var targetUserRoles = RoleCodes.Normalize(await _userManager.GetRolesAsync(targetUser));
        if (!targetUserRoles.Contains(roleCode, StringComparer.Ordinal))
        {
            throw new Common.Exceptions.ValidationException(new[]
            {
                new ValidationFailure(nameof(request.RoleCode),
                    $"User does not hold the {roleCode} role on their account — " +
                    "a project membership can only grant project scope for a role the account already has.")
            });
        }

        var now = DateTime.UtcNow;
        var validFrom = request.ValidFrom ?? now;
        if (request.ValidUntil.HasValue && request.ValidUntil.Value <= validFrom)
        {
            throw new Common.Exceptions.ValidationException(new[]
            {
                new ValidationFailure(nameof(request.ValidUntil), "ValidUntil must be after ValidFrom.")
            });
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var existingActive = await _db.Set<ProjectMembership>()
            .Where(m => m.ProjectId == request.ProjectId
                     && m.UserId == request.UserId
                     && m.RoleCode == roleCode
                     && m.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var membership in existingActive)
        {
            membership.IsActive = false;
        }

        var created = new ProjectMembership
        {
            Id = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            UserId = request.UserId,
            RoleCode = roleCode,
            ValidFrom = validFrom,
            ValidUntil = request.ValidUntil,
            IsActive = true,
            AssignedByUserId = _currentUser.UserId,
            AssignedAt = now
        };

        _db.Set<ProjectMembership>().Add(created);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CreateProjectMembershipResponse
        {
            Id = created.Id,
            Message = "Project membership created successfully."
        };
    }
}
