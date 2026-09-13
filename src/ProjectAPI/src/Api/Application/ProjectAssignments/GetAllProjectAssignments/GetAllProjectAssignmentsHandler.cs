using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.ProjectAssignments.GetProjectAssignmentById;
using ProjectAPI.Domain.Projects.Interfaces;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Api.Application.ProjectAssignments.GetAllProjectAssignments
{
    /// <summary>
    /// Legacy endpoint — reads from ProjectMembership (Phase 1's only source
    /// of truth), restricted to the two roles the old shape can represent
    /// (SALES_AGENT, NOTARY — TECHNICIAN and PROJECT_ADMIN memberships never
    /// appear here, since there is no legacy field for them), and mapped back
    /// onto AgentId/NotaryId.
    ///
    /// Phase 0 — a PROJECT_ADMIN only ever sees memberships for their own
    /// projects; GLOBAL_ADMIN is unrestricted (GetScopedProjectIdsAsync
    /// returns null).
    /// </summary>
    public class GetAllProjectAssignmentsHandler : IRequestHandler<GetAllProjectAssignmentsQuery, PaginatedResponse<GetProjectAssignmentByIdResponse>>
    {
        private readonly IProjectMembershipRepository _repository;
        private readonly ProjectScopeService _projectScope;

        public GetAllProjectAssignmentsHandler(IProjectMembershipRepository repository, ProjectScopeService projectScope)
        {
            _repository = repository;
            _projectScope = projectScope;
        }

        public async Task<PaginatedResponse<GetProjectAssignmentByIdResponse>> Handle(GetAllProjectAssignmentsQuery request, CancellationToken cancellationToken)
        {
            var scopedProjectIds = await _projectScope.GetScopedProjectIdsAsync(cancellationToken);

            var memberships = await _repository.Find(m =>
                (m.RoleCode == RoleCodes.SalesAgent || m.RoleCode == RoleCodes.Notary) &&
                (!request.ProjectId.HasValue || m.ProjectId == request.ProjectId.Value) &&
                (string.IsNullOrEmpty(request.AgentId) || (m.RoleCode == RoleCodes.SalesAgent && m.UserId == request.AgentId)) &&
                (string.IsNullOrEmpty(request.NotaryId) || (m.RoleCode == RoleCodes.Notary && m.UserId == request.NotaryId)) &&
                (scopedProjectIds == null || scopedProjectIds.Contains(m.ProjectId))
            );

            var totalItems = memberships.Count();

            var pagedMemberships = memberships
                // Stable order before paging: without it page contents are
                // nondeterministic and rows repeat or vanish between pages.
                .OrderByDescending(m => m.AssignedAt).ThenBy(m => m.Id)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(m => new GetProjectAssignmentByIdResponse
                {
                    Id = m.Id,
                    ProjectId = m.ProjectId,
                    AgentId = m.RoleCode == RoleCodes.SalesAgent ? m.UserId : null,
                    NotaryId = m.RoleCode == RoleCodes.Notary ? m.UserId : null,
                    CreatedAt = m.AssignedAt,
                    IsActive = m.IsActive
                })
                .ToList();

            return new PaginatedResponse<GetProjectAssignmentByIdResponse>(pagedMemberships, request.PageNumber, request.PageSize, totalItems);
        }
    }
}
