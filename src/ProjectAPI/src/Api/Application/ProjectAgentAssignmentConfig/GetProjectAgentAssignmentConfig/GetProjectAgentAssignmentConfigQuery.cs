namespace ProjectAPI.Api.Application.ProjectAgentAssignmentConfig.GetProjectAgentAssignmentConfig;

public class GetProjectAgentAssignmentConfigQuery : IRequest<GetProjectAgentAssignmentConfigResponse?>
{
    public Guid ProjectId { get; set; }
}
