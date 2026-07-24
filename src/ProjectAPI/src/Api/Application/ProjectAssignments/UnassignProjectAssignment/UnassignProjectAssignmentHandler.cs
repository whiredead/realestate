namespace ProjectAPI.Api.Application.ProjectAssignments.UnassignProjectAssignment;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Projects.Interfaces;

public class UnassignProjectAssignmentHandler
    : IRequestHandler<UnassignProjectAssignmentCommand, UnassignProjectAssignmentResponse>
{
    private readonly IProjectAssignmentRepository _repository;

    public UnassignProjectAssignmentHandler(IProjectAssignmentRepository repository)
        => _repository = repository;

    public async Task<UnassignProjectAssignmentResponse> Handle(
        UnassignProjectAssignmentCommand request,
        CancellationToken cancellationToken)
    {
        // 1) fetch
        var assignment = await _repository.GetByIDAsync(request.AssignmentId)
            ?? throw new NotFoundException(
                $"Project assignment {request.AssignmentId} not found.");

        // 2) deactivate
        assignment.IsActive = false;
        _repository.Update(assignment);
        await _repository.SaveAsync();

        // 3) respond
        return new UnassignProjectAssignmentResponse
        {
            Success = true,
            Message = "Assignment successfully deactivated."
        };
    }
}

