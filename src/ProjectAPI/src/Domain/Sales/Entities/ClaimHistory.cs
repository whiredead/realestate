namespace ProjectAPI.Domain.Sales.Entities;

public class ClaimHistory
{
    public Guid Id { get; set; }
    public Guid ClaimId { get; set; }
    public ClaimStatus FromStatus { get; set; }
    public ClaimStatus ToStatus { get; set; }
    public string ChangedByUserId { get; set; }
    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public AfterSaleClaim Claim { get; set; }
}
