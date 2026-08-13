using ProjectAPI.Api.Application.Agents.SetAgentWeeklyAvailability;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Users.Interfaces;

namespace ProjectAPI.Api.Application.Agents.GetAgentWeeklyAvailability;

public class GetAgentWeeklyAvailabilityHandler
    : IRequestHandler<GetAgentWeeklyAvailabilityQuery, List<WeeklyAvailabilitySlot>>
{
    private readonly IAgentWeeklyAvailabilityRepository _repo;
    private readonly ProjectScopeService _projectScope;

    public GetAgentWeeklyAvailabilityHandler(IAgentWeeklyAvailabilityRepository repo, ProjectScopeService projectScope)
    {
        _repo = repo;
        _projectScope = projectScope;
    }

    public async Task<List<WeeklyAvailabilitySlot>> Handle(GetAgentWeeklyAvailabilityQuery q, CancellationToken ct)
    {
        // §6.3 — AgentId comes from the route; a SALES_AGENT caller must only
        // read their own weekly availability, never another agent's.
        _projectScope.EnsureAgentOwnsCalendar(q.AgentId);

        var items = await _repo.Find(w => w.AgentId == q.AgentId);
        return items
            .OrderBy(w => w.DayOfWeek).ThenBy(w => w.StartTime)
            .Select(w => new WeeklyAvailabilitySlot
            {
                DayOfWeek = w.DayOfWeek,
                StartTime = w.StartTime,
                EndTime = w.EndTime
            }).ToList();
    }
}
