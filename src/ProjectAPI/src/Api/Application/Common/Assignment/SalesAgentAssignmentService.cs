using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.Crm.Entities;
using ProjectAPI.Domain.FinalVisits.Entities;
using ProjectAPI.Domain.Projects.Interfaces;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;
using ConfigEntity = ProjectAPI.Domain.Projects.Entities.ProjectAgentAssignmentConfig;

namespace ProjectAPI.Api.Application.Common.Assignment;

public class SalesAgentAssignmentService : ISalesAgentAssignmentService
{
    private readonly ApplicationDbContext _db;
    private readonly IProjectMembershipRepository _membershipRepo;
    private readonly IProjectAgentAssignmentConfigRepository _configRepo;

    public SalesAgentAssignmentService(
        ApplicationDbContext db,
        IProjectMembershipRepository membershipRepo,
        IProjectAgentAssignmentConfigRepository configRepo)
    {
        _db = db;
        _membershipRepo = membershipRepo;
        _configRepo = configRepo;
    }

    public async Task<AgentAssignmentResult> AssignAsync(
        Guid projectId, CrmContact contact, DateTime slotStart, DateTime slotEnd, CancellationToken ct)
    {
        // Rule 1 — existing responsible agent, but only if that relationship
        // is still current for THIS project: OwnerSalesAgentId is stored
        // per-contact (not per-project), so an owner from a different
        // project's history must not silently carry over here.
        if (!string.IsNullOrWhiteSpace(contact.OwnerSalesAgentId))
        {
            var ownerStillEligible = await IsActiveSalesAgentAsync(projectId, contact.OwnerSalesAgentId, ct);
            if (ownerStillEligible && await IsAvailableAsync(contact.OwnerSalesAgentId, slotStart, slotEnd, ct))
            {
                return new AgentAssignmentResult { AgentId = contact.OwnerSalesAgentId, AssignmentSource = "EXISTING_OWNER" };
            }
        }

        // Rule 2 — the project's configured automatic rule.
        var config = (await _configRepo.Find(c => c.ProjectId == projectId)).FirstOrDefault();
        if (config == null)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.NoEligibleAgentFound,
                "Ce projet n'a pas de règle d'affectation d'agent configurée.");
        }

        var eligibleAgentIds = await GetEligibleAgentIdsAsync(projectId, ct);
        if (eligibleAgentIds.Count == 0)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.NoEligibleAgentFound,
                "Aucun agent commercial actif n'est affecté à ce projet.");
        }

        return config.RuleType switch
        {
            "PRIMARY_AGENT" => await AssignPrimaryAgentAsync(config, eligibleAgentIds, slotStart, slotEnd, ct),
            "ROUND_ROBIN" => await AssignRoundRobinAsync(config, eligibleAgentIds, slotStart, slotEnd, ct),
            "LOWEST_WORKLOAD" => await AssignLowestWorkloadAsync(eligibleAgentIds, slotStart, slotEnd, ct),
            _ => throw new BusinessRuleException(
                BusinessErrorCodes.NoEligibleAgentFound,
                $"Type de règle d'affectation inconnu : {config.RuleType}.")
        };
    }

    private async Task<AgentAssignmentResult> AssignPrimaryAgentAsync(
        ConfigEntity config, List<string> eligibleAgentIds, DateTime slotStart, DateTime slotEnd, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(config.PrimaryAgentId)
            && eligibleAgentIds.Contains(config.PrimaryAgentId)
            && await IsAvailableAsync(config.PrimaryAgentId, slotStart, slotEnd, ct))
        {
            return new AgentAssignmentResult { AgentId = config.PrimaryAgentId, AssignmentSource = "PRIMARY_AGENT" };
        }

        // Primary agent unavailable for this slot — the UI should generally
        // only ever offer slots the primary agent can take, but if this is
        // reached anyway, fall back to lowest workload among the project's
        // other eligible agents rather than hard-failing the booking.
        return await AssignLowestWorkloadAsync(eligibleAgentIds, slotStart, slotEnd, ct);
    }

    private async Task<AgentAssignmentResult> AssignRoundRobinAsync(
        ConfigEntity config, List<string> eligibleAgentIds, DateTime slotStart, DateTime slotEnd, CancellationToken ct)
    {
        var ring = eligibleAgentIds.OrderBy(id => id, StringComparer.Ordinal).ToList();

        var startIndex = 0;
        if (!string.IsNullOrWhiteSpace(config.LastAssignedAgentId))
        {
            var lastIndex = ring.IndexOf(config.LastAssignedAgentId);
            if (lastIndex >= 0) startIndex = (lastIndex + 1) % ring.Count;
        }

        for (var i = 0; i < ring.Count; i++)
        {
            var candidate = ring[(startIndex + i) % ring.Count];
            if (await IsAvailableAsync(candidate, slotStart, slotEnd, ct))
            {
                config.LastAssignedAgentId = candidate;
                config.LastAssignedAt = DateTime.UtcNow;
                await _configRepo.Update(config);
                return new AgentAssignmentResult { AgentId = candidate, AssignmentSource = "ROUND_ROBIN" };
            }
        }

        throw new BusinessRuleException(
            BusinessErrorCodes.NoEligibleAgentFound,
            "Aucun agent n'est disponible pour ce créneau.");
    }

    private async Task<AgentAssignmentResult> AssignLowestWorkloadAsync(
        List<string> eligibleAgentIds, DateTime slotStart, DateTime slotEnd, CancellationToken ct)
    {
        var blockingStatuses = AppointmentStateMachine.BlockingStatuses.Select(s => s.ToString()).ToArray();

        // Live count, not PerformanceIndicator.AppointmentsScheduled — that
        // counter is cumulative and never decrements, so it cannot answer
        // "who has the least on their plate right now."
        var workloads = await _db.Set<Appointment>()
            .Where(a => a.SalesAgentId != null
                        && eligibleAgentIds.Contains(a.SalesAgentId)
                        && blockingStatuses.Contains(a.Status))
            .GroupBy(a => a.SalesAgentId)
            .Select(g => new { AgentId = g.Key!, Count = g.Count() })
            .ToListAsync(ct);

        var workloadByAgent = eligibleAgentIds.ToDictionary(id => id, id => 0);
        foreach (var w in workloads) workloadByAgent[w.AgentId] = w.Count;

        foreach (var candidate in workloadByAgent.OrderBy(kv => kv.Value).Select(kv => kv.Key))
        {
            if (await IsAvailableAsync(candidate, slotStart, slotEnd, ct))
            {
                return new AgentAssignmentResult { AgentId = candidate, AssignmentSource = "LOWEST_WORKLOAD" };
            }
        }

        throw new BusinessRuleException(
            BusinessErrorCodes.NoEligibleAgentFound,
            "Aucun agent n'est disponible pour ce créneau.");
    }

    private async Task<List<string>> GetEligibleAgentIdsAsync(Guid projectId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var memberships = await _membershipRepo.Find(m =>
            m.ProjectId == projectId &&
            m.RoleCode == RoleCodes.SalesAgent &&
            m.IsActive &&
            m.ValidFrom <= now &&
            (m.ValidUntil == null || m.ValidUntil > now));

        return memberships.Select(m => m.UserId).Distinct().ToList();
    }

    private async Task<bool> IsActiveSalesAgentAsync(Guid projectId, string agentId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var memberships = await _membershipRepo.Find(m =>
            m.ProjectId == projectId &&
            m.UserId == agentId &&
            m.RoleCode == RoleCodes.SalesAgent &&
            m.IsActive &&
            m.ValidFrom <= now &&
            (m.ValidUntil == null || m.ValidUntil > now));

        return memberships.Any();
    }

    /// <summary>
    /// Same ±1-minute overlap check CreateAppointmentHandler already applies
    /// to a manually-supplied agent — kept identical so automatic and manual
    /// assignment agree on what "available" means for a single instant.
    /// </summary>
    private async Task<bool> IsAvailableAsync(string agentId, DateTime slotStart, DateTime slotEnd, CancellationToken ct)
    {
        var blockingStatuses = AppointmentStateMachine.BlockingStatuses.Select(s => s.ToString()).ToArray();
        var conflictWindowStart = slotStart.AddMinutes(-1);
        var conflictWindowEnd = slotStart.AddMinutes(1);

        var hasConflict = await _db.Set<Appointment>().AnyAsync(a =>
            a.SalesAgentId == agentId &&
            blockingStatuses.Contains(a.Status) &&
            a.AppointmentDate > conflictWindowStart &&
            a.AppointmentDate < conflictWindowEnd, ct);

        return !hasConflict;
    }
}
