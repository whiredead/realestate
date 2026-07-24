using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Api.Application.ProjectAssignments.GetProjectAssignmentById;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.ProjectAssignments.GetAllProjectAssignments
{
    /// <summary>
    /// Handler to retrieve a paginated list of project assignments.
    /// </summary>
    public class GetAllProjectAssignmentsHandler : IRequestHandler<GetAllProjectAssignmentsQuery, PaginatedResponse<GetProjectAssignmentByIdResponse>>
    {
        private readonly IProjectAssignmentRepository _repository;

        public GetAllProjectAssignmentsHandler(IProjectAssignmentRepository repository)
        {
            _repository = repository;
        }

        public async Task<PaginatedResponse<GetProjectAssignmentByIdResponse>> Handle(GetAllProjectAssignmentsQuery request, CancellationToken cancellationToken)
        {
            var assignments = await _repository.Find(a =>
                (!request.ProjectId.HasValue || a.ProjectId == request.ProjectId.Value) &&
                (string.IsNullOrEmpty(request.AgentId) || a.AgentId == request.AgentId) &&
                (string.IsNullOrEmpty(request.NotaryId) || a.NotaryId == request.NotaryId)
            );

            var totalItems = assignments.Count();

            var pagedAssignments = assignments
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(a => new GetProjectAssignmentByIdResponse
                {
                    Id = a.Id,
                    ProjectId = a.ProjectId,
                    AgentId = a.AgentId ?? string.Empty,
                    NotaryId = a.NotaryId ?? string.Empty,
                    CreatedAt = a.CreatedAt,
                    IsActive = a.IsActive
                })
                .ToList();

            return new PaginatedResponse<GetProjectAssignmentByIdResponse>(pagedAssignments, request.PageNumber, request.PageSize, totalItems);
        }
    }
}
