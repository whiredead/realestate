using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Immeubles.Interfaces;

namespace ProjectAPI.Api.Application.Immeubles.CreateIImmeubleFeature;

public class AddImmeubleFeatureHandler : IRequestHandler<AddImmeubleFeatureCommand, bool>
{
    private readonly IImmeubleFeatureRepository _repository;

    public AddImmeubleFeatureHandler(IImmeubleFeatureRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(AddImmeubleFeatureCommand request, CancellationToken cancellationToken)
    {
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