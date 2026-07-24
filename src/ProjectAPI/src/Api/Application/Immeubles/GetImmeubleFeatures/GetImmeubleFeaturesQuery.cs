using ProjectAPI.Api.Application.Common.Models;

namespace ProjectAPI.Api.Application.Immeubles.GetImmeubleFeatures;

public class GetImmeubleFeaturesQuery : IRequest<List<ImmeubleFeatureResponse>>
{
    public Guid ImmeubleId { get; set; }
}