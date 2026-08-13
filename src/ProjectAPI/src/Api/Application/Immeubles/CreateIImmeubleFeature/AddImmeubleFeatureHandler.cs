using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Immeubles.Interfaces;

namespace ProjectAPI.Api.Application.Immeubles.CreateIImmeubleFeature;

public class AddImmeubleFeatureHandler : IRequestHandler<AddImmeubleFeatureCommand, bool>
{
    private readonly IImmeubleFeatureRepository _repository;
    private readonly IImmeubleRepository _immeubleRepository;
    private readonly ProjectScopeService _projectScope;

    public AddImmeubleFeatureHandler(
        IImmeubleFeatureRepository repository,
        IImmeubleRepository immeubleRepository,
        ProjectScopeService projectScope)
    {
        _repository = repository;
        _immeubleRepository = immeubleRepository;
        _projectScope = projectScope;
    }

    public async Task<bool> Handle(AddImmeubleFeatureCommand request, CancellationToken cancellationToken)
    {
        var immeuble = await _immeubleRepository.GetByIDAsync(request.ImmeubleId)
            ?? throw new NotFoundException($"Immeuble with ID {request.ImmeubleId} not found.");

        await _projectScope.EnsureProjectAccessAsync(immeuble.ProjectId, cancellationToken);

        var features = request.Features.Select(f => new ImmeubleFeature
        {
            Id = Guid.NewGuid(),
            ImmeubleId = request.ImmeubleId,
            Name = f.Name,
            Icon = f.Icon
        }).ToList();

        foreach (var feature in features)
            await _repository.InsertAsync(feature);
        await _repository.SaveAsync();
        return true;
    }
}