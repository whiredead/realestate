using ProjectAPI.Api.Application.Common.Models;

namespace ProjectAPI.Api.Application.Projects.GetProjectFeatures;

public class GetProjectFeaturesQuery : IRequest<List<ProjectFeatureResponse>>
{
    public Guid ProjectId { get; set; }
}