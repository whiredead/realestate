using FluentValidation.Results;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.Projects.UpdateProjectFeature;

public class UpdateProjectFeatureHandler : IRequestHandler<UpdateProjectFeatureCommand, ProjectFeatureResponse>
{
    private readonly IProjectFeatureRepository _repository;
    private readonly ProjectScopeService _projectScope;

    public UpdateProjectFeatureHandler(IProjectFeatureRepository repository, ProjectScopeService projectScope)
    {
        _repository = repository;
        _projectScope = projectScope;
    }

    public async Task<ProjectFeatureResponse> Handle(UpdateProjectFeatureCommand request, CancellationToken ct)
    {
        var feature = await _repository.GetByIDAsync(request.Id)
            ?? throw new NotFoundException($"Project feature {request.Id} not found.");

        // §6.4 — the feature's own ProjectId decides the perimeter, not
        // whatever a caller might pass: this command carries no ProjectId at
        // all, so there is nothing to trust but the stored row.
        await _projectScope.EnsureProjectAccessAsync(feature.ProjectId, ct);

        var failures = new List<ValidationFailure>();
        if (string.IsNullOrWhiteSpace(request.Name)) failures.Add(new ValidationFailure("Name", "Le nom est obligatoire."));
        else if (request.Name.Trim().Length > 150) failures.Add(new ValidationFailure("Name", "Le nom ne peut dépasser 150 caractères."));
        if (string.IsNullOrWhiteSpace(request.Icon)) failures.Add(new ValidationFailure("Icon", "L'icône est obligatoire."));
        if (request.Description is { Length: > 300 }) failures.Add(new ValidationFailure("Description", "La description ne peut dépasser 300 caractères."));
        if (failures.Count > 0) throw new Common.Exceptions.ValidationException(failures);

        feature.Name = request.Name!.Trim();
        feature.Icon = request.Icon!.Trim();
        feature.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        await _repository.Update(feature);
        await _repository.SaveAsync();

        return new ProjectFeatureResponse
        {
            Id = feature.Id,
            Name = feature.Name,
            Icon = feature.Icon,
            Description = feature.Description,
            SequenceNo = feature.SequenceNo,
            ProjectId = feature.ProjectId
        };
    }
}
