using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Projects.Interfaces;
using ProjectAPI.Domain.Users.Entities;
using ConfigEntity = ProjectAPI.Domain.Projects.Entities.ProjectAgentAssignmentConfig;

namespace ProjectAPI.Api.Application.ProjectAgentAssignmentConfig.SetProjectAgentAssignmentConfig;

public class SetProjectAgentAssignmentConfigHandler
    : IRequestHandler<SetProjectAgentAssignmentConfigCommand, SetProjectAgentAssignmentConfigResponse>
{
    private static readonly string[] ValidRuleTypes = { "ROUND_ROBIN", "LOWEST_WORKLOAD", "PRIMARY_AGENT" };

    private readonly IProjectAgentAssignmentConfigRepository _configRepo;
    private readonly IProjectMembershipRepository _membershipRepo;
    private readonly ProjectScopeService _projectScope;

    public SetProjectAgentAssignmentConfigHandler(
        IProjectAgentAssignmentConfigRepository configRepo,
        IProjectMembershipRepository membershipRepo,
        ProjectScopeService projectScope)
    {
        _configRepo = configRepo;
        _membershipRepo = membershipRepo;
        _projectScope = projectScope;
    }

    public async Task<SetProjectAgentAssignmentConfigResponse> Handle(SetProjectAgentAssignmentConfigCommand req, CancellationToken ct)
    {
        // §6.4 — the caller's own perimeter, not just whether PrimaryAgentId
        // has standing on the project. Without this a scoped PROJECT_ADMIN
        // could rewrite another project's lead-routing rule entirely.
        await _projectScope.EnsureProjectAccessAsync(req.ProjectId, ct);

        if (!ValidRuleTypes.Contains(req.RuleType))
            throw new ArgumentException($"RuleType must be one of: {string.Join(", ", ValidRuleTypes)}.");

        if (req.RuleType == "PRIMARY_AGENT")
        {
            if (string.IsNullOrWhiteSpace(req.PrimaryAgentId))
                throw new ArgumentException("PrimaryAgentId is required when RuleType is PRIMARY_AGENT.");

            await EnsureActiveSalesAgentAsync(req.ProjectId, req.PrimaryAgentId);
        }

        var existing = (await _configRepo.Find(c => c.ProjectId == req.ProjectId)).FirstOrDefault();

        if (existing != null)
        {
            existing.RuleType = req.RuleType;
            existing.PrimaryAgentId = req.RuleType == "PRIMARY_AGENT" ? req.PrimaryAgentId : null;
            existing.UpdatedByUserId = req.UpdatedByUserId;
            existing.UpdatedAt = DateTime.UtcNow;
            await _configRepo.Update(existing);
        }
        else
        {
            existing = new ConfigEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = req.ProjectId,
                RuleType = req.RuleType,
                PrimaryAgentId = req.RuleType == "PRIMARY_AGENT" ? req.PrimaryAgentId : null,
                UpdatedByUserId = req.UpdatedByUserId,
                UpdatedAt = DateTime.UtcNow
            };
            await _configRepo.InsertAsync(existing);
        }

        await _configRepo.SaveAsync();

        return new SetProjectAgentAssignmentConfigResponse
        {
            ProjectId = existing.ProjectId,
            RuleType = existing.RuleType,
            PrimaryAgentId = existing.PrimaryAgentId
        };
    }

    /// <summary>
    /// Every agent this config can ever point at (PrimaryAgentId here, and
    /// later any manually-reassigned agent) must actually hold an active
    /// SALES_AGENT ProjectMembership for this exact project — otherwise the
    /// config could name someone with no real standing on the project at all.
    /// </summary>
    private async Task EnsureActiveSalesAgentAsync(Guid projectId, string agentId)
    {
        var now = DateTime.UtcNow;
        var memberships = await _membershipRepo.Find(m =>
            m.ProjectId == projectId &&
            m.UserId == agentId &&
            m.RoleCode == RoleCodes.SalesAgent &&
            m.IsActive &&
            m.ValidFrom <= now &&
            (m.ValidUntil == null || m.ValidUntil > now));

        if (!memberships.Any())
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.AgentNotEligibleForProject,
                $"L'agent {agentId} n'a pas d'affectation SALES_AGENT active sur ce projet.");
        }
    }
}
