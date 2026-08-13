using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Projects.Interfaces;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Api.Application.ProjectAssignments.GetProjectAssignmentById
{
    /// <summary>
    /// Legacy endpoint — reads from ProjectMembership (Phase 1's only source
    /// of truth) and maps the result back onto the old AgentId/NotaryId
    /// shape: a SALES_AGENT membership becomes AgentId, a NOTARY membership
    /// becomes NotaryId. request.Id is a ProjectMembership.Id.
    /// </summary>
    public class GetProjectAssignmentByIdHandler : IRequestHandler<GetProjectAssignmentByIdQuery, GetProjectAssignmentByIdResponse>
    {
        private readonly IProjectMembershipRepository _repository;
        private readonly ProjectScopeService _projectScope;

        public GetProjectAssignmentByIdHandler(IProjectMembershipRepository repository, ProjectScopeService projectScope)
        {
            _repository = repository;
            _projectScope = projectScope;
        }

        public async Task<GetProjectAssignmentByIdResponse> Handle(GetProjectAssignmentByIdQuery request, CancellationToken cancellationToken)
        {
            var membership = await _repository.GetByIDAsync(request.Id);
            if (membership == null)
            {
                throw new NotFoundException($"Project assignment with ID {request.Id} not found.");
            }

            await _projectScope.EnsureProjectAccessAsync(membership.ProjectId, cancellationToken);

            return new GetProjectAssignmentByIdResponse
            {
                Id = membership.Id,
                ProjectId = membership.ProjectId,
                AgentId = membership.RoleCode == RoleCodes.SalesAgent ? membership.UserId : null,
                NotaryId = membership.RoleCode == RoleCodes.Notary ? membership.UserId : null,
                CreatedAt = membership.AssignedAt,
                IsActive = membership.IsActive
            };
        }
    }
}
