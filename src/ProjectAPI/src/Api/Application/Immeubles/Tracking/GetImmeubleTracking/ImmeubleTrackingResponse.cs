namespace ProjectAPI.Api.Application.Immeubles.Tracking.GetImmeubleTracking;
public class ImmeubleTrackingResponse
{
    public Guid Id { get; set; }
    public Guid ImmeubleId { get; set; }
    public string StatusUpdate { get; set; }
    public DateTime DateUpdated { get; set; }
}
