using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Domain.Users.Interfaces;

namespace ProjectAPI.Api.Application.Agents.SetAgentDateOverride;

public class SetAgentDateOverrideHandler
    : IRequestHandler<SetAgentDateOverrideCommand, SetAgentDateOverrideResponse>
{
    private readonly IAgentDateOverrideRepository _repo;
    private readonly ProjectScopeService _projectScope;

    public SetAgentDateOverrideHandler(IAgentDateOverrideRepository repo, ProjectScopeService projectScope)
    {
        _repo = repo;
        _projectScope = projectScope;
    }

    public async Task<SetAgentDateOverrideResponse> Handle(SetAgentDateOverrideCommand req, CancellationToken ct)
    {
        // §6.3 — AgentId comes from the route; a SALES_AGENT caller must only
        // set date overrides on their own calendar, never another agent's.
        _projectScope.EnsureAgentOwnsCalendar(req.AgentId);

        if (req.EndDate < req.StartDate)
            throw new ArgumentException("EndDate must not be before StartDate.");
        if (req.Status != "Available" && req.Status != "Unavailable")
            throw new ArgumentException("Status must be 'Available' or 'Unavailable'.");

        var entity = new AgentDateOverride
        {
            Id = Guid.NewGuid(),
            AgentId = req.AgentId,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            Status = req.Status,
            DateCreation = DateTime.UtcNow
        };

        await _repo.InsertAsync(entity);
        await _repo.SaveAsync();

        return new SetAgentDateOverrideResponse
        {
            Id = entity.Id,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Status = entity.Status
        };
    }
}
