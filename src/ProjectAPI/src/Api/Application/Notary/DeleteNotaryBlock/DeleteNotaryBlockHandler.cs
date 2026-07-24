using ProjectAPI.Domain.Users.Interfaces;

namespace ProjectAPI.Api.Application.Notary.DeleteNotaryBlock;

public class DeleteNotaryBlockHandler : IRequestHandler<DeleteNotaryBlockCommand, bool>
{
    private readonly INotaryBlockRepository _blockRepo;

    public DeleteNotaryBlockHandler(INotaryBlockRepository blockRepo)
    {
        _blockRepo = blockRepo;
    }

    public async Task<bool> Handle(DeleteNotaryBlockCommand req, CancellationToken ct)
    {
        var blocks = await _blockRepo.Find(b => b.Id == req.BlockId && b.NotaryId == req.NotaryId);
        var block = blocks.FirstOrDefault();
        if (block == null) return false;

        _blockRepo.Delete(block);
        await _blockRepo.SaveAsync();
        return true;
    }
}
