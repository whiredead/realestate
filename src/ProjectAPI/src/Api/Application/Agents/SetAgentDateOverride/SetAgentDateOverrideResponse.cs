namespace ProjectAPI.Api.Application.Agents.SetAgentDateOverride;

public class SetAgentDateOverrideResponse
{
    public Guid Id { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = null!;
}
