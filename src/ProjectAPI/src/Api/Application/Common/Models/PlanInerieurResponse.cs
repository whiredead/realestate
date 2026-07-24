namespace ProjectAPI.Api.Application.Common.Models;

public class PlanInerieurResponse
{
    public Guid Id { get; set; }
    public Guid? ImmeubleId { get; set; }
    public List<string> PhotoLinks { get; set; } = new List<string>();
}
