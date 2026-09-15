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

        var existing = await _repository.Find(f => f.ProjectId == request.ProjectId);
        var nextSequence = (existing.Any() ? existing.Max(f => f.SequenceNo) : 0) + 1;
        var now = DateTime.UtcNow;

        var features = request.Features.Select((f, i) => new ProjectFeature
        {
            Id = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            Name = f.Name,
            Icon = f.Icon,
            Description = string.IsNullOrWhiteSpace(f.Description) ? null : f.Description.Trim(),
            SequenceNo = nextSequence + i,
            CreatedAt = now
        }).ToList();

        foreach(var feature in features)
            await _repository.InsertAsync(feature);
        await _repository.SaveAsync();
        return true;
    }
}