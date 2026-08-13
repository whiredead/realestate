using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.ProjectMemberships.EndProjectMembership;

/// <summary>Phase 1 — project-scoped: an admin may only end a membership inside their own perimeter.</summary>
public class EndProjectMembershipHandler : IRequestHandler<EndProjectMembershipCommand, EndProjectMembershipResponse>
{
    private readonly IProjectMembershipRepository _repository;
    private readonly ProjectScopeService _projectScope;

    public EndProjectMembershipHandler(IProjectMembershipRepository repository, ProjectScopeService projectScope)
    {
        _repository = repository;
        _projectScope = projectScope;
    }

    public async Task<EndProjectMembershipResponse> Handle(EndProjectMembershipCommand request, CancellationToken cancellationToken)
    {
        var membership = await _repository.GetByIDAsync(request.Id)
            ?? throw new NotFoundException($"Project membership {request.Id} not found.");

        await _projectScope.EnsureProjectAccessAsync(membership.ProjectId, cancellationToken);

        membership.IsActive = false;
        await _repository.Update(membership);
        await _repository.SaveAsync();

        return new EndProjectMembershipResponse
        {
            Success = true,
            Message = "Project membership deactivated successfully."
        };
    }
}
