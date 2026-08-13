namespace ProjectAPI.Api.Application.Agents.SetAgentWeeklyAvailability;

/// <summary>Replaces an agent's entire recurring weekly schedule with the given slots.</summary>
public class SetAgentWeeklyAvailabilityCommand : IRequest<List<WeeklyAvailabilitySlot>>
{
    // Nullable: always supplied by the controller from the route, not the
    // request body — a non-nullable string here makes [ApiController]'s
    // implicit model validation reject any body that omits it, even though
    // the controller overwrites it before dispatch either way.
    public string? AgentId { get; set; }
    public List<WeeklyAvailabilitySlot> Slots { get; set; } = new();
}
