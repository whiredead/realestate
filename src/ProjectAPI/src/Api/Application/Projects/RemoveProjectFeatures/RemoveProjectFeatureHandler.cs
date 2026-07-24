using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.Projects.RemoveProjectFeatures;

public class RemoveProjectFeatureHandler
    : IRequestHandler<RemoveProjectFeatureCommand, bool>
{
    private readonly IProjectFeatureRepository _repository;

    public RemoveProjectFeatureHandler(IProjectFeatureRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(
        RemoveProjectFeatureCommand request,
        CancellationToken cancellationToken)
    {
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
