using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Immeubles.Interfaces;

namespace ProjectAPI.Api.Application.Immeubles.Tracking.AddImmeubleTracking;

public class AddImmeubleTrackingHandler : IRequestHandler<AddImmeubleTrackingCommand, bool>
{
    private readonly IImmeubleTrackingRepository _repository;

    public AddImmeubleTrackingHandler(IImmeubleTrackingRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(AddImmeubleTrackingCommand request, CancellationToken cancellationToken)
    {
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