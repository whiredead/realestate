namespace ProjectAPI.Domain.Reservations.Entities;

public class ReservationDocument
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }

    public string FileName { get; set; } = null!;
    public string Url { get; set; } = null!;          // blob/object storage location
    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }

    public string? DocumentType { get; set; }         // e.g. "Blueprint", "CIN", "Contract"
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string? UploadedBy { get; set; }           // admin id

    public Reservation Reservation { get; set; } = null!;
}