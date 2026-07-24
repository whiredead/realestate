using ProjectAPI.Domain.Immeubles.Interfaces;

namespace ProjectAPI.Api.Application.Immeubles.Tracking.GetImmeubleTracking;

public class GetImmeubleTrackingHandler : IRequestHandler<GetImmeubleTrackingQuery, List<ImmeubleTrackingResponse>>
{
    private readonly IImmeubleTrackingRepository _repository;

    public GetImmeubleTrackingHandler(IImmeubleTrackingRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<ImmeubleTrackingResponse>> Handle(GetImmeubleTrackingQuery request, CancellationToken cancellationToken)
    {
        var trackings = await _repository.Find(it => it.ImmeubleId == request.ImmeubleId);

        return trackings.Select(it => new ImmeubleTrackingResponse
        {
            Id = it.Id,
            ImmeubleId = it.ImmeubleId,
            StatusUpdate = it.StatusUpdate,
            DateUpdated = it.DateUpdated
        }).ToList();
    }
}
