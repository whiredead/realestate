namespace ProjectAPI.Api.Application.Agents.SetAgentAppointmentSettings;

public class SetAgentAppointmentSettingsResponse
{
    public string AgentId { get; set; } = null!;
    public int DurationMinutes { get; set; }
    public int BufferMinutes { get; set; }
}
