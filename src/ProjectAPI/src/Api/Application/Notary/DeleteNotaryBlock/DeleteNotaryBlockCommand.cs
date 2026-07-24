namespace ProjectAPI.Api.Application.Notary.DeleteNotaryBlock;

public class DeleteNotaryBlockCommand : IRequest<bool>
{
    public string NotaryId { get; set; } = null!;
    public Guid BlockId { get; set; }
}
