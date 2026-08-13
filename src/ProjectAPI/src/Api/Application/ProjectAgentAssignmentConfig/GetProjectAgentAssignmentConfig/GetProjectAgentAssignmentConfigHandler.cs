using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.ProjectAgentAssignmentConfig.GetProjectAgentAssignmentConfig;

public class GetProjectAgentAssignmentConfigHandler
    : IRequestHandler<GetProjectAgentAssignmentConfigQuery, GetProjectAgentAssignmentConfigResponse?>
{
    private readonly IProjectAgentAssignmentConfigRepository _configRepo;
    private readonly ProjectScopeService _projectScope;

    public GetProjectAgentAssignmentConfigHandler(IProjectAgentAssignmentConfigRepository configRepo, ProjectScopeService projectScope)
    {
        _configRepo = configRepo;
        _projectScope = projectScope;
    }

    public async Task<GetProjectAgentAssignmentConfigResponse?> Handle(GetProjectAgentAssignmentConfigQuery req, CancellationToken ct)
    {
        // §6.4 — reads another project's lead-routing rule (incl. its
        // PrimaryAgentId) otherwise, the read side of the same gap.
        await _projectScope.EnsureProjectAccessAsync(req.ProjectId, ct);

        var config = (await _configRepo.Find(c => c.ProjectId == req.ProjectId)).FirstOrDefault();
        if (config == null) return null;

        return new GetProjectAgentAssignmentConfigResponse
        {
            ProjectId = config.ProjectId,
            RuleType = config.RuleType,
            PrimaryAgentId = config.PrimaryAgentId,
            LastAssignedAgentId = config.LastAssignedAgentId,
            LastAssignedAt = config.LastAssignedAt
        };
    }
}
