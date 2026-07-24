namespace ProjectAPI.Api.Application.Notary.GetNotaryCalendar;

public class GetNotaryCalendarQuery
 : IRequest<List<NotaryDayAvailabilityDto>>
{
    public string NotaryId { get; init; } = null!;
    public DateTime From { get; init; }
    public DateTime To { get; init; }
}

public record TimeSlotDto(TimeSpan Start, TimeSpan End);

public class NotaryDayAvailabilityDto
{
    public DateOnly Date { get; init; }
    public List<TimeSlotDto> FreeSlots { get; init; } = new();
}
