namespace ProjectAPI.Api.Application.Immeubles.CreateIImmeubleFeature;

public class AddImmeubleFeatureCommand : IRequest<bool>
{
    public Guid ImmeubleId { get; set; }
    public List<ImmeubleFeatureRequest> Features { get; set; }
}
