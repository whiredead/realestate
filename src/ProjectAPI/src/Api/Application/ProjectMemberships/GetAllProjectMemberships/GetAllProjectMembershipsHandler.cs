using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.ProjectMemberships.GetProjectMembershipById;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.ProjectMemberships.GetAllProjectMemberships;

/// <summary>
/// Phase 1 — a PROJECT_ADMIN only ever sees memberships for their own
/// projects; GLOBAL_ADMIN is unrestricted (GetScopedProjectIdsAsync returns
/// null). Mirrors GetAllProjectAssignmentsHandler.
/// </summary>
public class GetAllProjectMembershipsHandler : IRequestHandler<GetAllProjectMembershipsQuery, PaginatedResponse<GetProjectMembershipByIdResponse>>
{
    private readonly IProjectMembershipRepository _repository;
    private readonly ProjectScopeService _projectScope;

    public GetAllProjectMembershipsHandler(IProjectMembershipRepository repository, ProjectScopeService projectScope)
    {
        _repository = repository;
        _projectScope = projectScope;
    }

    public async Task<PaginatedResponse<GetProjectMembershipByIdResponse>> Handle(GetAllProjectMembershipsQuery request, CancellationToken cancellationToken)
    {
        var scopedProjectIds = await _projectScope.GetScopedProjectIdsAsync(cancellationToken);

        var memberships = await _repository.Find(m =>
            (!request.ProjectId.HasValue || m.ProjectId == request.ProjectId.Value) &&
            (string.IsNullOrEmpty(request.UserId) || m.UserId == request.UserId) &&
            (string.IsNullOrEmpty(request.RoleCode) || m.RoleCode == request.RoleCode) &&
            (scopedProjectIds == null || scopedProjectIds.Contains(m.ProjectId))
        );

        var totalItems = memberships.Count();

        var pagedMemberships = memberships
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => new GetProjectMembershipByIdResponse
            {
                Id = m.Id,
                ProjectId = m.ProjectId,
                UserId = m.UserId,
                RoleCode = m.RoleCode,
                ValidFrom = m.ValidFrom,
                ValidUntil = m.ValidUntil,
                IsActive = m.IsActive,
                AssignedByUserId = m.AssignedByUserId,
                AssignedAt = m.AssignedAt
            })
            .ToList();

        return new PaginatedResponse<GetProjectMembershipByIdResponse>(pagedMemberships, request.PageNumber, request.PageSize, totalItems);
    }
}
