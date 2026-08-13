using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.ProjectAssignments.CreateProjectAssignment
{
    /// <summary>
    /// Legacy endpoint — translates an agent/notary assignment request into a
    /// ProjectMembership write. ProjectMembership is the ONLY writable source
    /// of truth (Phase 1): this handler no longer touches the ProjectAssignments
    /// table at all, so there is no second, independently-maintained copy of
    /// staffing data to drift out of sync. The legacy ProjectAssignments table
    /// is kept only for its migration history (see BackfillProjectMembershipsFromAssignments)
    /// until every caller of this controller has moved to
    /// ProjectMembershipController.
    ///
    /// The returned Id is a ProjectMembership.Id, not a ProjectAssignment.Id —
    /// callers of this legacy endpoint must pass it straight back into
    /// Update/Delete/Unassign, which look it up the same way.
    ///
    /// Same role-match guard as CreateProjectMembershipHandler: AgentId must
    /// actually hold SALES_AGENT and NotaryId must actually hold NOTARY on
    /// their account before either can be granted project scope for that
    /// role.
    /// </summary>
    public class CreateProjectAssignmentHandler : IRequestHandler<CreateProjectAssignmentCommand, CreateProjectAssignmentResponse>
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<User> _userManager;
        private readonly ProjectScopeService _projectScope;
        private readonly ICurrentUser _currentUser;

        public CreateProjectAssignmentHandler(
            ApplicationDbContext db, UserManager<User> userManager, ProjectScopeService projectScope, ICurrentUser currentUser)
        {
            _db = db;
            _userManager = userManager;
            _projectScope = projectScope;
            _currentUser = currentUser;
        }

        public async Task<CreateProjectAssignmentResponse> Handle(CreateProjectAssignmentCommand request, CancellationToken cancellationToken)
        {
            await _projectScope.EnsureProjectAccessAsync(request.ProjectId, cancellationToken);

            var agentId = request.AgentId.IsNullOrEmpty() || request.AgentId == "string" ? null : request.AgentId;
            var notaryId = request.NotaryId.IsNullOrEmpty() || request.NotaryId == "string" ? null : request.NotaryId;

            // The legacy shape allows either or both to be set; this handler
            // has always only ever been called with exactly one populated in
            // practice (the frontend sends one of the two), but if both are
            // present it creates two membership rows, one per role — same as
            // the Phase 1 backfill did for historical rows with both set.
            if (string.IsNullOrEmpty(agentId) && string.IsNullOrEmpty(notaryId))
            {
                throw new Common.Exceptions.ValidationException(new[]
                {
                    new FluentValidation.Results.ValidationFailure(nameof(request.AgentId), "Either AgentId or NotaryId is required.")
                });
            }

            if (!string.IsNullOrEmpty(agentId))
            {
                await EnsureUserHoldsRoleAsync(agentId, RoleCodes.SalesAgent, nameof(request.AgentId));
            }
            if (!string.IsNullOrEmpty(notaryId))
            {
                await EnsureUserHoldsRoleAsync(notaryId, RoleCodes.Notary, nameof(request.NotaryId));
            }

            var now = DateTime.UtcNow;
            Guid createdId = Guid.Empty;

            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            async Task<Guid> UpsertAsync(string userId, string roleCode)
            {
                var existingActive = await _db.Set<ProjectMembership>()
                    .Where(m => m.ProjectId == request.ProjectId && m.UserId == userId && m.RoleCode == roleCode && m.IsActive)
                    .ToListAsync(cancellationToken);

                foreach (var existing in existingActive)
                {
                    existing.IsActive = false;
                }

                var membership = new ProjectMembership
                {
                    Id = Guid.NewGuid(),
                    ProjectId = request.ProjectId,
                    UserId = userId,
                    RoleCode = roleCode,
                    ValidFrom = now,
                    ValidUntil = null,
                    IsActive = request.IsActive,
                    AssignedByUserId = _currentUser.UserId,
                    AssignedAt = now
                };
                _db.Set<ProjectMembership>().Add(membership);
                return membership.Id;
            }

            if (!string.IsNullOrEmpty(agentId))
            {
                createdId = await UpsertAsync(agentId, RoleCodes.SalesAgent);
            }
            if (!string.IsNullOrEmpty(notaryId))
            {
                createdId = await UpsertAsync(notaryId, RoleCodes.Notary);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new CreateProjectAssignmentResponse
            {
                Id = createdId,
                Message = "Project assignment created successfully."
            };
        }

        private async Task EnsureUserHoldsRoleAsync(string userId, string roleCode, string fieldName)
        {
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new Common.Exceptions.NotFoundException($"User {userId} not found.");

            var userRoles = RoleCodes.Normalize(await _userManager.GetRolesAsync(user));
            if (!userRoles.Contains(roleCode, StringComparer.Ordinal))
            {
                throw new Common.Exceptions.ValidationException(new[]
                {
                    new FluentValidation.Results.ValidationFailure(fieldName,
                        $"User does not hold the {roleCode} role on their account — " +
                        "a project assignment can only grant project scope for a role the account already has.")
                });
            }
        }
    }
}
