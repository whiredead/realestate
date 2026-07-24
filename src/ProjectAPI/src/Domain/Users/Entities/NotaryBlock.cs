namespace ProjectAPI.Domain.Users.Entities;

public class NotaryBlock
{
    public Guid Id { get; set; }
    public string NotaryId { get; set; }
    public DateTime Start { get; set; }    // exact UTC
    public DateTime End { get; set; }
    public string Reason { get; set; }    // optional

    public Notary Notary { get; set; } = null!;
}
