using ProjectAPI.Domain.Crm.Entities;

namespace ProjectAPI.Api.Application.Common.Assignment;

public class AgentAssignmentResult
{
    public required string AgentId { get; init; }
    /// <summary>"EXISTING_OWNER" | "ROUND_ROBIN" | "LOWEST_WORKLOAD" | "PRIMARY_AGENT".</summary>
    public required string AssignmentSource { get; init; }
}

/// <summary>
/// Picks the sales agent for a new commercial appointment (spec: assignment
/// flow + agent selection rules). Priority order:
///   1. Existing responsible agent — the prospect's CrmContact.OwnerSalesAgentId,
///      if that agent still holds an active SALES_AGENT membership on this
///      project and is available for the requested slot.
///   2. The project's configured automatic rule (ProjectAgentAssignmentConfig):
///      ROUND_ROBIN, LOWEST_WORKLOAD, or PRIMARY_AGENT.
/// Manual assignment (rule 3 in the spec) is NOT this service's job — an
/// admin explicitly picking/reassigning an agent bypasses this entirely; see
/// CreateAppointmentHandler and ReassignAppointment.
/// </summary>
public interface ISalesAgentAssignmentService
{
    /// <summary>
    /// Throws BusinessRuleException(NoEligibleAgentFound) if no active
    /// SALES_AGENT membership exists for the project, or every eligible
    /// agent is unavailable for the requested slot.
    /// </summary>
    Task<AgentAssignmentResult> AssignAsync(
        Guid projectId,
        CrmContact contact,
        DateTime slotStart,
        DateTime slotEnd,
        CancellationToken ct);
}
