using MediatR;

namespace ProjectAPI.Api.Application.Projects.RemoveProject;

public class RemoveProjectCommand : IRequest<RemoveProjectResponse>
{
    public Guid ProjectId { get; set; }
}