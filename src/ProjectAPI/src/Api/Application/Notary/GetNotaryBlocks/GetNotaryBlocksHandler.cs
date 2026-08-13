using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Users.Interfaces;

namespace ProjectAPI.Api.Application.Notary.GetNotaryBlocks;

public class GetNotaryBlocksHandler :
  IRequestHandler<GetNotaryBlocksQuery, List<NotaryBlockDto>>
{
    private readonly INotaryBlockRepository _blockRepo;
    private readonly ProjectScopeService _projectScope;

    public GetNotaryBlocksHandler(INotaryBlockRepository blockRepo, ProjectScopeService projectScope)
    {
        _blockRepo = blockRepo;
        _projectScope = projectScope;
    }

    public async Task<List<NotaryBlockDto>> Handle(GetNotaryBlocksQuery q, CancellationToken ct)
    {
        // §6.3 — NotaryId comes from the route; a NOTARY caller must only
        // read their own blocks, never another notary's calendar.
        _projectScope.EnsureNotaryOwnsCalendar(q.NotaryId);

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
