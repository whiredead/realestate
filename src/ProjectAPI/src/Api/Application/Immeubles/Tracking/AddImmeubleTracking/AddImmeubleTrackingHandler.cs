using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Immeubles.Interfaces;

namespace ProjectAPI.Api.Application.Immeubles.Tracking.AddImmeubleTracking;

public class AddImmeubleTrackingHandler : IRequestHandler<AddImmeubleTrackingCommand, bool>
{
    private readonly IImmeubleTrackingRepository _repository;
    private readonly IImmeubleRepository _immeubleRepository;
    private readonly ProjectScopeService _projectScope;

    public AddImmeubleTrackingHandler(
        IImmeubleTrackingRepository repository,
        IImmeubleRepository immeubleRepository,
        ProjectScopeService projectScope)
    {
        _repository = repository;
        _immeubleRepository = immeubleRepository;
        _projectScope = projectScope;
    }

    public async Task<bool> Handle(AddImmeubleTrackingCommand request, CancellationToken cancellationToken)
    {
        var immeuble = await _immeubleRepository.GetByIDAsync(request.ImmeubleId)
            ?? throw new NotFoundException($"Immeuble with ID {request.ImmeubleId} not found.");

        await _projectScope.EnsureProjectAccessAsync(immeuble.ProjectId, cancellationToken);

        var tracking = new ImmeubleTracking
        {
            Id = Guid.NewGuid(),
            ImmeubleId = request.ImmeubleId,
            StatusUpdate = request.StatusUpdate,
            DateUpdated = DateTime.Now
        };

        await _repository.InsertAsync(tracking);
        await _repository.SaveAsync();
        return true;
    }
}