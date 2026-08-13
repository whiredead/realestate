using Microsoft.AspNetCore.Identity;
using ProjectAPI.Domain.Projects.Interfaces;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;

namespace ProjectAPI.Api.Application.ProjectAssignments.UpdateProjectAssignment
{
    /// <summary>
    /// Legacy endpoint — request.Id is a ProjectMembership.Id (see
    /// CreateProjectAssignmentHandler). Updates the membership row directly;
    /// there is no separate ProjectAssignment row to keep in sync, so nothing
    /// can drift.
    ///
    /// Phase 0 — a PROJECT_ADMIN may only touch a membership that already
    /// belongs to their own perimeter, AND may not move it to a project
    /// outside that perimeter either: both the membership's current project
    /// and, when the caller is changing it, the target project are checked.
    ///
    /// Same role-match guard as CreateProjectAssignmentHandler: whichever of
    /// AgentId/NotaryId is supplied must actually hold that role on their
    /// account.
    /// </summary>
    public class UpdateProjectAssignmentHandler : IRequestHandler<UpdateProjectAssignmentCommand, UpdateProjectAssignmentResponse>
    {
        private readonly IProjectMembershipRepository _repository;
        private readonly UserManager<User> _userManager;
        private readonly ProjectScopeService _projectScope;

        public UpdateProjectAssignmentHandler(
            IProjectMembershipRepository repository, UserManager<User> userManager, ProjectScopeService projectScope)
        {
            _repository = repository;
            _userManager = userManager;
            _projectScope = projectScope;
        }

        public async Task<UpdateProjectAssignmentResponse> Handle(UpdateProjectAssignmentCommand request, CancellationToken cancellationToken)
        {
            var membership = await _repository.GetByIDAsync(request.Id);
            if (membership == null)
            {
                throw new NotFoundException($"Project assignment with ID {request.Id} not found.");
            }

            await _projectScope.EnsureProjectAccessAsync(membership.ProjectId, cancellationToken);
            if (request.ProjectId.HasValue && request.ProjectId.Value != membership.ProjectId)
            {
                await _projectScope.EnsureProjectAccessAsync(request.ProjectId.Value, cancellationToken);
            }

            // The legacy shape lets a caller flip which role this row
            // represents (AgentId vs NotaryId) — translate that onto
            // RoleCode/UserId; whichever of the two is supplied wins, same
            // precedence the old entity had (both columns lived on one row).
            if (!string.IsNullOrWhiteSpace(request.AgentId))
            {
                await EnsureUserHoldsRoleAsync(request.AgentId, RoleCodes.SalesAgent, nameof(request.AgentId));
                membership.UserId = request.AgentId;
                membership.RoleCode = RoleCodes.SalesAgent;
            }
            else if (!string.IsNullOrWhiteSpace(request.NotaryId))
            {
                await EnsureUserHoldsRoleAsync(request.NotaryId, RoleCodes.Notary, nameof(request.NotaryId));
                membership.UserId = request.NotaryId;
                membership.RoleCode = RoleCodes.Notary;
            }

            if (request.ProjectId.HasValue)
            {
                membership.ProjectId = request.ProjectId.Value;
            }

            if (request.IsActive.HasValue)
            {
                membership.IsActive = request.IsActive.Value;
            }

            await _repository.Update(membership);
            await _repository.SaveAsync();

            return new UpdateProjectAssignmentResponse
            {
                Id = membership.Id,
                Message = "Project assignment updated successfully."
            };
        }

        private async Task EnsureUserHoldsRoleAsync(string userId, string roleCode, string fieldName)
        {
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new NotFoundException($"User {userId} not found.");

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
