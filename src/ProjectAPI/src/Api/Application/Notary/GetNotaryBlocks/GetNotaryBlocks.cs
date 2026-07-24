namespace ProjectAPI.Api.Application.Notary.GetNotaryBlocks;

public class GetNotaryBlocksQuery : IRequest<List<NotaryBlockDto>>
{
    public string NotaryId { get; set; } = null!;
    public DateTime? From { get; set; } // UTC
    public DateTime? To { get; set; } // UTC
}
