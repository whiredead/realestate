using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Users.Interfaces;

namespace ProjectAPI.Api.Application.Notary.DeleteNotaryBlock;

public class DeleteNotaryBlockHandler : IRequestHandler<DeleteNotaryBlockCommand, bool>
{
    private readonly INotaryBlockRepository _blockRepo;
    private readonly ProjectScopeService _projectScope;

    public DeleteNotaryBlockHandler(INotaryBlockRepository blockRepo, ProjectScopeService projectScope)
    {
        _blockRepo = blockRepo;
        _projectScope = projectScope;
    }

    public async Task<bool> Handle(DeleteNotaryBlockCommand req, CancellationToken ct)
    {
        // §6.3 — NotaryId comes from the route; a NOTARY caller must be
        // deleting their own block, never another notary's.
        _projectScope.EnsureNotaryOwnsCalendar(req.NotaryId);

        var blocks = await _blockRepo.Find(b => b.Id == req.BlockId && b.NotaryId == req.NotaryId);
        var block = blocks.FirstOrDefault();
        if (block == null) return false;

        _blockRepo.Delete(block);
        await _blockRepo.SaveAsync();
        return true;
    }
}
