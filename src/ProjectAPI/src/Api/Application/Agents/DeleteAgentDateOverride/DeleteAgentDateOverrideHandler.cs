using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Users.Interfaces;

namespace ProjectAPI.Api.Application.Agents.DeleteAgentDateOverride;

public class DeleteAgentDateOverrideHandler : IRequestHandler<DeleteAgentDateOverrideCommand, bool>
{
    private readonly IAgentDateOverrideRepository _repo;
    private readonly ProjectScopeService _projectScope;

    public DeleteAgentDateOverrideHandler(IAgentDateOverrideRepository repo, ProjectScopeService projectScope)
    {
        _repo = repo;
        _projectScope = projectScope;
    }

    public async Task<bool> Handle(DeleteAgentDateOverrideCommand req, CancellationToken ct)
    {
        // §6.3 — AgentId comes from the route; a SALES_AGENT caller must be
        // deleting their own date override, never another agent's.
        _projectScope.EnsureAgentOwnsCalendar(req.AgentId);

        var rows = await _repo.Find(o => o.Id == req.OverrideId && o.AgentId == req.AgentId);
        var row = rows.FirstOrDefault();
        if (row == null) return false;

        _repo.Delete(row);
        await _repo.SaveAsync();
        return true;
    }
}
