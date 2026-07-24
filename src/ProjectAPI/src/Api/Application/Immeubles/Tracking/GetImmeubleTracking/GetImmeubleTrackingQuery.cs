namespace ProjectAPI.Api.Application.Immeubles.Tracking.GetImmeubleTracking;

public class GetImmeubleTrackingQuery : IRequest<List<ImmeubleTrackingResponse>>
{
    public Guid ImmeubleId { get; set; }
}

