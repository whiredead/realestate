namespace ProjectAPI.Api.Application.Projects.AddProjectFratures;

public class AddProjectFeatureCommand : IRequest<bool>
{
    public Guid ProjectId { get; set; }
    public List<ProjectFeatureRequest> Features { get; set; }
}