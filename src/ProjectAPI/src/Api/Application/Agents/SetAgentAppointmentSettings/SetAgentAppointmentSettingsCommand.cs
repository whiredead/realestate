namespace ProjectAPI.Api.Application.Agents.SetAgentAppointmentSettings;

public class SetAgentAppointmentSettingsCommand : IRequest<SetAgentAppointmentSettingsResponse>
{
    // Nullable: supplied by the controller from the route, not the body.
    public string? AgentId { get; set; }
    public int DurationMinutes { get; set; }
    public int BufferMinutes { get; set; }
}
