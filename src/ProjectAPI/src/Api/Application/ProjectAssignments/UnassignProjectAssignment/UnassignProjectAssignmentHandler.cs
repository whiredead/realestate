namespace ProjectAPI.Api.Application.ProjectAssignments.UnassignProjectAssignment;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Projects.Interfaces;

/// <summary>
/// Legacy endpoint — request.AssignmentId is a ProjectMembership.Id.
/// Deactivates the membership directly (see UpdateProjectAssignmentHandler).
/// </summary>
public class UnassignProjectAssignmentHandler
    : IRequestHandler<UnassignProjectAssignmentCommand, UnassignProjectAssignmentResponse>
{
    private readonly IProjectMembershipRepository _repository;
    private readonly ProjectScopeService _projectScope;

    public UnassignProjectAssignmentHandler(IProjectMembershipRepository repository, ProjectScopeService projectScope)
    {
        _repository = repository;
        _projectScope = projectScope;
    }

    public async Task<UnassignProjectAssignmentResponse> Handle(
        UnassignProjectAssignmentCommand request,
        CancellationToken cancellationToken)
    {
        var membership = await _repository.GetByIDAsync(request.AssignmentId)
            ?? throw new NotFoundException(
                $"Project assignment {request.AssignmentId} not found.");

        await _projectScope.EnsureProjectAccessAsync(membership.ProjectId, cancellationToken);

        membership.IsActive = false;
        await _repository.Update(membership);
        await _repository.SaveAsync();

        return new UnassignProjectAssignmentResponse
        {
            Success = true,
            Message = "Assignment successfully deactivated."
        };
    }
}
