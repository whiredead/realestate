using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Users.Interfaces;

namespace ProjectAPI.Api.Application.Agents.DeleteAgentBlock;

public class DeleteAgentBlockHandler : IRequestHandler<DeleteAgentBlockCommand, bool>
{
    private readonly IAgentBlockRepository _blockRepo;
    private readonly ProjectScopeService _projectScope;

    public DeleteAgentBlockHandler(IAgentBlockRepository blockRepo, ProjectScopeService projectScope)
    {
        _blockRepo = blockRepo;
        _projectScope = projectScope;
    }

    public async Task<bool> Handle(DeleteAgentBlockCommand req, CancellationToken ct)
    {
        // §6.3 — AgentId comes from the route; a SALES_AGENT caller must be
        // deleting their own block, never another agent's.
        _projectScope.EnsureAgentOwnsCalendar(req.AgentId);

        var blocks = await _blockRepo.Find(b => b.Id == req.BlockId && b.AgentId == req.AgentId);
        var block = blocks.FirstOrDefault();
        if (block == null) return false;

        _blockRepo.Delete(block);
        await _blockRepo.SaveAsync();
        return true;
    }
}
