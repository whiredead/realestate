using ProjectAPI.Domain.Users.Interfaces;

namespace ProjectAPI.Api.Application.Notary.GetNotaryBlocks;

public class GetNotaryBlocksHandler :
  IRequestHandler<GetNotaryBlocksQuery, List<NotaryBlockDto>>
{
    private readonly INotaryBlockRepository _blockRepo;

    public GetNotaryBlocksHandler(INotaryBlockRepository blockRepo) => _blockRepo = blockRepo;

    public async Task<List<NotaryBlockDto>> Handle(GetNotaryBlocksQuery q, CancellationToken ct)
    {
        var from = q.From ?? DateTime.MinValue;
        var to = q.To ?? DateTime.MaxValue;

        var items = await _blockRepo.Find(b =>
            b.NotaryId == q.NotaryId && b.End > from && b.Start < to);

        return items.Select(b => new NotaryBlockDto
        {
            Id = b.Id,
            StartUtc = b.Start,
            EndUtc = b.End,
            Reason = b.Reason
        }).OrderBy(x => x.StartUtc).ToList();
    }
}
