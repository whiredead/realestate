using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Domain.Immeubles.Interfaces;

namespace ProjectAPI.Api.Application.Immeubles.GetImmeubleFeatures;

public class GetImmeubleFeaturesHandler : IRequestHandler<GetImmeubleFeaturesQuery, List<ImmeubleFeatureResponse>>
{
    private readonly IImmeubleFeatureRepository _repository;

    public GetImmeubleFeaturesHandler(IImmeubleFeatureRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<ImmeubleFeatureResponse>> Handle(GetImmeubleFeaturesQuery request, CancellationToken cancellationToken)
    {
        var features = await _repository.Find(f => f.ImmeubleId == request.ImmeubleId);
        return features.Select(f => new ImmeubleFeatureResponse
        {
            Id = f.Id,
            Name = f.Name,
            Icon = f.Icon
        }).ToList();
    }
}
