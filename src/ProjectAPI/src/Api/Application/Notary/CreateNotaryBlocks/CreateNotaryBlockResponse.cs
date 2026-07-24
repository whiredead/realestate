namespace ProjectAPI.Api.Application.Notary.CreateNotaryBlocks;

public class CreateNotaryBlockResponse
{
    public Guid Id { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string? Reason { get; set; }
}
