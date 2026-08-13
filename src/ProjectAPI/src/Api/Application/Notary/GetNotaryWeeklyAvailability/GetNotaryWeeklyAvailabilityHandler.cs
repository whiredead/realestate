using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Notary.SetNotaryWeeklyAvailability;
using ProjectAPI.Domain.Users.Interfaces;

namespace ProjectAPI.Api.Application.Notary.GetNotaryWeeklyAvailability;

public class GetNotaryWeeklyAvailabilityHandler
    : IRequestHandler<GetNotaryWeeklyAvailabilityQuery, List<NotaryWeeklyAvailabilitySlot>>
{
    private readonly IWeeklyAvailabilityRepository _repo;
    private readonly ProjectScopeService _projectScope;

    public GetNotaryWeeklyAvailabilityHandler(IWeeklyAvailabilityRepository repo, ProjectScopeService projectScope)
    {
        _repo = repo;
        _projectScope = projectScope;
    }

    public async Task<List<NotaryWeeklyAvailabilitySlot>> Handle(GetNotaryWeeklyAvailabilityQuery q, CancellationToken ct)
    {
        // §6.3 — NotaryId comes from the route; a NOTARY caller must only
        // read their own weekly availability, never another notary's.
        _projectScope.EnsureNotaryOwnsCalendar(q.NotaryId);

        var items = await _repo.Find(w => w.NotaryId == q.NotaryId);
        return items
            .OrderBy(w => w.DayOfWeek).ThenBy(w => w.StartTime)
            .Select(w => new NotaryWeeklyAvailabilitySlot
            {
                DayOfWeek = w.DayOfWeek,
                StartTime = w.StartTime,
                EndTime = w.EndTime
            }).ToList();
    }
}
