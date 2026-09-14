namespace ProjectAPI.Domain.Sales.Entities;

public class ClaimComment
{
    public Guid Id { get; set; }
    public Guid ClaimId { get; set; }
    public string AuthorUserId { get; set; }     // agent or buyer
    public string Message { get; set; }

    /// <summary>WORK_DONE (technician's work report), NOTE (staff) or REPLY (buyer).</summary>
    public string Kind { get; set; } = "NOTE";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AfterSaleClaim Claim { get; set; }
}
