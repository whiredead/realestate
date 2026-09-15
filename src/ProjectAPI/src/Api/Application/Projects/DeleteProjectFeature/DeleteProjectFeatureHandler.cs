using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.Projects.DeleteProjectFeature;

public class DeleteProjectFeatureHandler : IRequestHandler<DeleteProjectFeatureCommand, MediatR.Unit>
{
    private readonly IProjectFeatureRepository _repository;
    private readonly ProjectScopeService _projectScope;

    public DeleteProjectFeatureHandler(IProjectFeatureRepository repository, ProjectScopeService projectScope)
    {
        _repository = repository;
        _projectScope = projectScope;
    }

    public async Task<MediatR.Unit> Handle(DeleteProjectFeatureCommand request, CancellationToken ct)
    {
        var feature = await _repository.GetByIDAsync(request.Id)
            ?? throw new NotFoundException($"Project feature {request.Id} not found.");

        await _projectScope.EnsureProjectAccessAsync(feature.ProjectId, ct);

        _repository.Delete(feature);
        await _repository.SaveAsync();
        return MediatR.Unit.Value;
    }
}
