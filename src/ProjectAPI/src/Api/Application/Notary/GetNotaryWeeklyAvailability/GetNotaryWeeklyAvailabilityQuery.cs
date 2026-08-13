using ProjectAPI.Api.Application.Notary.SetNotaryWeeklyAvailability;

namespace ProjectAPI.Api.Application.Notary.GetNotaryWeeklyAvailability;

public class GetNotaryWeeklyAvailabilityQuery : IRequest<List<NotaryWeeklyAvailabilitySlot>>
{
    public string NotaryId { get; set; } = null!;
}
