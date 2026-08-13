using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Users.Interfaces;

namespace ProjectAPI.Api.Application.Agents.GetAgentBlocks;

public class GetAgentBlocksHandler :
  IRequestHandler<GetAgentBlocksQuery, List<AgentBlockDto>>
{
    private readonly IAgentBlockRepository _blockRepo;
    private readonly ProjectScopeService _projectScope;

    public GetAgentBlocksHandler(IAgentBlockRepository blockRepo, ProjectScopeService projectScope)
    {
        _blockRepo = blockRepo;
        _projectScope = projectScope;
    }

    public async Task<List<AgentBlockDto>> Handle(GetAgentBlocksQuery q, CancellationToken ct)
    {
        // §6.3 — AgentId comes from the route; a SALES_AGENT caller must only
        // read their own blocks, never another agent's calendar.
        _projectScope.EnsureAgentOwnsCalendar(q.AgentId);

        var from = q.From ?? DateTime.MinValue;
        var to = q.To ?? DateTime.MaxValue;

        var items = await _blockRepo.Find(b =>
            b.AgentId == q.AgentId && b.End > from && b.Start < to);

        return items.Select(b => new AgentBlockDto
        {
            Id = b.Id,
            StartUtc = b.Start,
            EndUtc = b.End,
            Reason = b.Reason
        }).OrderBy(x => x.StartUtc).ToList();
    }
}
