using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.Projects.AddProjectFratures;

public class AddProjectFeatureHandler : IRequestHandler<AddProjectFeatureCommand, bool>
{
    private readonly IProjectFeatureRepository _repository;

    public AddProjectFeatureHandler(IProjectFeatureRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(AddProjectFeatureCommand request, CancellationToken cancellationToken)
    {
        var features = request.Features.Select(f => new ProjectFeature
        {
            Id = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            Name = f.Name,
            Icon = f.Icon
        }).ToList();

        foreach(var feature in features)
            await _repository.InsertAsync(feature);
        await _repository.SaveAsync();
        return true;
    }
}