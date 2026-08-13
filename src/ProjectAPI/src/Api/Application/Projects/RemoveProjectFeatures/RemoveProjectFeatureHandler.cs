using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.Projects.RemoveProjectFeatures;

public class RemoveProjectFeatureHandler
    : IRequestHandler<RemoveProjectFeatureCommand, bool>
{
    private readonly IProjectFeatureRepository _repository;
    private readonly ProjectScopeService _projectScope;

    public RemoveProjectFeatureHandler(IProjectFeatureRepository repository, ProjectScopeService projectScope)
    {
        _repository = repository;
        _projectScope = projectScope;
    }

    public async Task<bool> Handle(
        RemoveProjectFeatureCommand request,
        CancellationToken cancellationToken)
    {
        // §6.4 — request.ProjectId is caller-supplied; a PROJECT_ADMIN with
        // no membership on it must not delete another project's features.
        await _projectScope.EnsureProjectAccessAsync(request.ProjectId, cancellationToken);

        // 1) load all the matching features
        var toDelete = (await _repository.Find(f =>
                f.ProjectId == request.ProjectId
             && request.FeatureIds.Contains(f.Id)))
             .ToList();

        if (!toDelete.Any())
            throw new NotFoundException(
                $"No features found on project {request.ProjectId} with IDs {string.Join(",", request.FeatureIds)}");

        // 2) delete them
        foreach (var feat in toDelete)
            _repository.Delete(feat);

        // 3) persist
        await _repository.SaveAsync();
        return true;
    }
}
