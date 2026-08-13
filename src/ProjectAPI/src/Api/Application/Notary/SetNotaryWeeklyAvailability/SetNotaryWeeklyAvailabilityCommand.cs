namespace ProjectAPI.Api.Application.Notary.SetNotaryWeeklyAvailability;

/// <summary>
/// Replaces a notary's entire recurring weekly schedule with the given
/// slots. Mirrors SetAgentWeeklyAvailabilityCommand — the notary-side
/// WeeklyAvailability entity/read path (GetNotaryCalendarHandler) already
/// existed, but had no write endpoint at all until this.
/// </summary>
public class SetNotaryWeeklyAvailabilityCommand : IRequest<List<NotaryWeeklyAvailabilitySlot>>
{
    // Nullable: always supplied by the controller from the route, not the
    // request body — see AgentAvailabilityController's identical pattern.
    public string? NotaryId { get; set; }
    public List<NotaryWeeklyAvailabilitySlot> Slots { get; set; } = new();
}
