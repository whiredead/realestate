namespace ProjectAPI.Api.Application.Notary.CreateNotaryBlocks;

public class CreateNotaryBlockCommand : IRequest<CreateNotaryBlockResponse>
{
    public string NotaryId { get; init; } = null!;
    public DateTime Start { get; init; }
    public DateTime End { get; init; }
    public string Reason { get; init; } = "";
}
