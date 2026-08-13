using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.ProjectMemberships.GetProjectMembershipById;

public class GetProjectMembershipByIdHandler : IRequestHandler<GetProjectMembershipByIdQuery, GetProjectMembershipByIdResponse>
{
    private readonly IProjectMembershipRepository _repository;
    private readonly ProjectScopeService _projectScope;

    public GetProjectMembershipByIdHandler(IProjectMembershipRepository repository, ProjectScopeService projectScope)
    {
        _repository = repository;
        _projectScope = projectScope;
    }

    public async Task<GetProjectMembershipByIdResponse> Handle(GetProjectMembershipByIdQuery request, CancellationToken cancellationToken)
    {
        var membership = await _repository.GetByIDAsync(request.Id)
            ?? throw new NotFoundException($"Project membership {request.Id} not found.");

        await _projectScope.EnsureProjectAccessAsync(membership.ProjectId, cancellationToken);

        return new GetProjectMembershipByIdResponse
        {
            Id = membership.Id,
            ProjectId = membership.ProjectId,
            UserId = membership.UserId,
            RoleCode = membership.RoleCode,
            ValidFrom = membership.ValidFrom,
            ValidUntil = membership.ValidUntil,
            IsActive = membership.IsActive,
            AssignedByUserId = membership.AssignedByUserId,
            AssignedAt = membership.AssignedAt
        };
    }
}
