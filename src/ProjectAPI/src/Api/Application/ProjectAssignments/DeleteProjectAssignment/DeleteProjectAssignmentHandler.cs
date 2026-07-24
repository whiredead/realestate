using ProjectAPI.Domain.Projects.Interfaces;
using ProjectAPI.Api.Application.Common.Exceptions;

namespace ProjectAPI.Api.Application.ProjectAssignments.DeleteProjectAssignment
{
    /// <summary>
    /// Handler for deleting a project assignment.
    /// </summary>
    public class DeleteProjectAssignmentHandler : IRequestHandler<DeleteProjectAssignmentCommand, DeleteProjectAssignmentResponse>
    {
        private readonly IProjectAssignmentRepository _repository;

        public DeleteProjectAssignmentHandler(IProjectAssignmentRepository repository)
        {
            _repository = repository;
        }

        public async Task<DeleteProjectAssignmentResponse> Handle(DeleteProjectAssignmentCommand request, CancellationToken cancellationToken)
        {
            var assignment = await _repository.GetByIDAsync(request.Id);
            if (assignment == null)
            {
                throw new NotFoundException($"Project assignment with ID {request.Id} not found.");
            }

            _repository.Delete(assignment);
            await _repository.SaveAsync();

            return new DeleteProjectAssignmentResponse
            {
                Id = request.Id,
                Message = "Project assignment deleted successfully."
            };
        }
    }
}
