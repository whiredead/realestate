namespace ProjectAPI.Domain.Sales.Entities;

public class ClaimAttachment
{
    public Guid Id { get; set; }
    public Guid ClaimId { get; set; }
    public string Url { get; set; }           // blob URL
    public string? FileName { get; set; }
    public string? ContentType { get; set; }

    /// <summary>BEFORE / AFTER the intervention for technician photos; null for the buyer's evidence.</summary>
    public string? Phase { get; set; }  // image/jpeg, video/mp4, application/pdf...
    public long? SizeBytes { get; set; }
    public string? UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public AfterSaleClaim Claim { get; set; }
}