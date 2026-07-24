using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.ProjectAssignments.GetProjectAssignmentById
{
    /// <summary>
    /// Handler to retrieve a project assignment by its ID.
    /// </summary>
    public class GetProjectAssignmentByIdHandler : IRequestHandler<GetProjectAssignmentByIdQuery, GetProjectAssignmentByIdResponse>
    {
        private readonly IProjectAssignmentRepository _repository;

        public GetProjectAssignmentByIdHandler(IProjectAssignmentRepository repository)
        {
            _repository = repository;
        }

        public async Task<GetProjectAssignmentByIdResponse> Handle(GetProjectAssignmentByIdQuery request, CancellationToken cancellationToken)
        {
            var assignment = await _repository.GetByIDAsync(request.Id);
            if (assignment == null)
            {
                throw new NotFoundException($"Project assignment with ID {request.Id} not found.");
            }

            return new GetProjectAssignmentByIdResponse
            {
                Id = assignment.Id,
                ProjectId = assignment.ProjectId,
                AgentId = assignment.AgentId ?? string.Empty,
                NotaryId = assignment.NotaryId ?? string.Empty,
                CreatedAt = assignment.CreatedAt,
                IsActive = assignment.IsActive
            };
        }
    }
}
