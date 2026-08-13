using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.Projects.AddProjectFratures;

public class AddProjectFeatureHandler : IRequestHandler<AddProjectFeatureCommand, bool>
{
    private readonly IProjectFeatureRepository _repository;
    private readonly ProjectScopeService _projectScope;

    public AddProjectFeatureHandler(IProjectFeatureRepository repository, ProjectScopeService projectScope)
    {
        _repository = repository;
        _projectScope = projectScope;
    }

    public async Task<bool> Handle(AddProjectFeatureCommand request, CancellationToken cancellationToken)
    {
        // §6.4 — request.ProjectId is caller-supplied; a PROJECT_ADMIN with
        // no membership on it must not add features to another project's
        // catalogue entry.
        await _projectScope.EnsureProjectAccessAsync(request.ProjectId, cancellationToken);

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