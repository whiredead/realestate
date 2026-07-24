using Microsoft.IdentityModel.Tokens;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.ProjectAssignments.CreateProjectAssignment
{
    /// <summary>
    /// Handler for creating a new project assignment.
    /// </summary>
    public class CreateProjectAssignmentHandler : IRequestHandler<CreateProjectAssignmentCommand, CreateProjectAssignmentResponse>
    {
        private readonly IProjectAssignmentRepository _repository;

        public CreateProjectAssignmentHandler(IProjectAssignmentRepository repository)
        {
            _repository = repository;
        }

        public async Task<CreateProjectAssignmentResponse> Handle(CreateProjectAssignmentCommand request, CancellationToken cancellationToken)
        {
            var assignment = new ProjectAssignment
            {
                Id = Guid.NewGuid(),
                ProjectId = request.ProjectId,
                AgentId = request.AgentId.IsNullOrEmpty() || request.AgentId == "string" ? null : request.AgentId,
                NotaryId = request.NotaryId.IsNullOrEmpty() || request.NotaryId == "string" ? null:request.NotaryId,
                CreatedAt = DateTime.Now,
                IsActive = request.IsActive
            };

            await _repository.InsertAsync(assignment);
            await _repository.SaveAsync();

            return new CreateProjectAssignmentResponse
            {
                Id = assignment.Id,
                Message = "Project assignment created successfully."
            };
        }
    }
}
