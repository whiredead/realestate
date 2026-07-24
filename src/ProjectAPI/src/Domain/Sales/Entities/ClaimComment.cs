namespace ProjectAPI.Domain.Sales.Entities;

public class ClaimComment
{
    public Guid Id { get; set; }
    public Guid ClaimId { get; set; }
    public string AuthorUserId { get; set; }     // agent or buyer
    public string Message { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AfterSaleClaim Claim { get; set; }
}
