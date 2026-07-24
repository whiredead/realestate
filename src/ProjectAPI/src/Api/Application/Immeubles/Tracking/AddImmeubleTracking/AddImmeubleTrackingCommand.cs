namespace ProjectAPI.Api.Application.Immeubles.Tracking.AddImmeubleTracking;

public class AddImmeubleTrackingCommand : IRequest<bool>
{
    public Guid ImmeubleId { get; set; }
    public string StatusUpdate { get; set; }

}
