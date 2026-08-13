using ProjectAPI.Domain.Projects.Interfaces;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;

namespace ProjectAPI.Api.Application.ProjectAssignments.DeleteProjectAssignment
{
    /// <summary>
    /// Legacy endpoint — request.Id is a ProjectMembership.Id. Hard-deletes
    /// the membership row: ProjectMembership is now the only copy of this
    /// data (no separate ProjectAssignment row is kept in sync), so unlike
    /// the transitional shadow-write design this replaces, there is nothing
    /// else to reconcile.
    /// </summary>
    public class DeleteProjectAssignmentHandler : IRequestHandler<DeleteProjectAssignmentCommand, DeleteProjectAssignmentResponse>
    {
        private readonly IProjectMembershipRepository _repository;
        private readonly ProjectScopeService _projectScope;

        public DeleteProjectAssignmentHandler(IProjectMembershipRepository repository, ProjectScopeService projectScope)
        {
            _repository = repository;
            _projectScope = projectScope;
        }

        public async Task<DeleteProjectAssignmentResponse> Handle(DeleteProjectAssignmentCommand request, CancellationToken cancellationToken)
        {
            var membership = await _repository.GetByIDAsync(request.Id);
            if (membership == null)
            {
                throw new NotFoundException($"Project assignment with ID {request.Id} not found.");
            }

            await _projectScope.EnsureProjectAccessAsync(membership.ProjectId, cancellationToken);

            _repository.Delete(membership);
            await _repository.SaveAsync();

            return new DeleteProjectAssignmentResponse
            {
                Id = request.Id,
                Message = "Project assignment deleted successfully."
            };
        }
    }
}
