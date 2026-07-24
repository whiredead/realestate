using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.Projects.GetProjectFeatures;

public class GetProjectFeaturesHandler : IRequestHandler<GetProjectFeaturesQuery, List<ProjectFeatureResponse>>
{
    private readonly IProjectFeatureRepository _repository;

    public GetProjectFeaturesHandler(IProjectFeatureRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<ProjectFeatureResponse>> Handle(GetProjectFeaturesQuery request, CancellationToken cancellationToken)
    {
        var features = await _repository.Find(f => f.ProjectId == request.ProjectId);
        return features.Select(f => new ProjectFeatureResponse
        {
            Id = f.Id,
            Name = f.Name,
            Icon = f.Icon
        }).ToList();
    }
}