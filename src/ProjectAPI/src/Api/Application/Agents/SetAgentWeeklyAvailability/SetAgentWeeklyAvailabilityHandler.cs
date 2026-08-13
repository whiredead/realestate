using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Domain.Users.Interfaces;

namespace ProjectAPI.Api.Application.Agents.SetAgentWeeklyAvailability;

public class SetAgentWeeklyAvailabilityHandler
    : IRequestHandler<SetAgentWeeklyAvailabilityCommand, List<WeeklyAvailabilitySlot>>
{
    private readonly IAgentWeeklyAvailabilityRepository _repo;
    private readonly ProjectScopeService _projectScope;

    public SetAgentWeeklyAvailabilityHandler(IAgentWeeklyAvailabilityRepository repo, ProjectScopeService projectScope)
    {
        _repo = repo;
        _projectScope = projectScope;
    }

    public async Task<List<WeeklyAvailabilitySlot>> Handle(SetAgentWeeklyAvailabilityCommand req, CancellationToken ct)
    {
        // §6.3 — AgentId comes from the route; a SALES_AGENT caller must only
        // replace their own weekly schedule, never another agent's.
        _projectScope.EnsureAgentOwnsCalendar(req.AgentId);

        foreach (var slot in req.Slots)
        {
            if (slot.EndTime <= slot.StartTime)
                throw new ArgumentException($"EndTime must be after StartTime for {slot.DayOfWeek}.");
        }

        // Replace-all: this endpoint sets the whole week at once rather than
        // exposing granular per-slot CRUD, which the legacy Notary model never
        // needed a controller for either.
        var existing = await _repo.Find(w => w.AgentId == req.AgentId);
        foreach (var row in existing)
        {
            _repo.Delete(row);
        }

        foreach (var slot in req.Slots)
        {
            await _repo.InsertAsync(new AgentWeeklyAvailability
            {
                Id = Guid.NewGuid(),
                AgentId = req.AgentId,
                DayOfWeek = slot.DayOfWeek,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime
            });
        }

        await _repo.SaveAsync();

        return req.Slots;
    }
}
