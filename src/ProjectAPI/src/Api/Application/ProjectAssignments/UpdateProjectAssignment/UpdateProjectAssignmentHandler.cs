using ProjectAPI.Domain.Projects.Interfaces;
using ProjectAPI.Api.Application.Common.Exceptions;

namespace ProjectAPI.Api.Application.ProjectAssignments.UpdateProjectAssignment
{
    /// <summary>
    /// Handler for updating an existing project assignment.
    /// </summary>
    public class UpdateProjectAssignmentHandler : IRequestHandler<UpdateProjectAssignmentCommand, UpdateProjectAssignmentResponse>
    {
        private readonly IProjectAssignmentRepository _repository;

        public UpdateProjectAssignmentHandler(IProjectAssignmentRepository repository)
        {
            _repository = repository;
        }

        public async Task<UpdateProjectAssignmentResponse> Handle(UpdateProjectAssignmentCommand request, CancellationToken cancellationToken)
        {
            var assignment = await _repository.GetByIDAsync(request.Id);
            if (assignment == null)
            {
                throw new NotFoundException($"Project assignment with ID {request.Id} not found.");
            }

            // Update fields if they are provided (basic example)
            if (!string.IsNullOrWhiteSpace(request.AgentId))
                assignment.AgentId = request.AgentId;
            else
                assignment.AgentId = null;

            if (!string.IsNullOrWhiteSpace(request.NotaryId))
                assignment.NotaryId = request.NotaryId;
            else
                assignment.NotaryId = request.NotaryId;

            if (request.ProjectId.HasValue)
            {
                assignment.ProjectId = request.ProjectId.Value;
            }

            if (request.IsActive.HasValue)
            {
                assignment.IsActive = request.IsActive.Value;
            }

            await _repository.Update(assignment);
            await _repository.SaveAsync();

            return new UpdateProjectAssignmentResponse
            {
                Id = assignment.Id,
                Message = "Project assignment updated successfully."
            };
        }
    }
}
