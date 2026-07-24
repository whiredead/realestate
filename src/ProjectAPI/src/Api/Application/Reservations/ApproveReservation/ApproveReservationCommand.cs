namespace ProjectAPI.Api.Application.Reservations.ApproveReservation;

public class ApproveReservationCommand : IRequest<bool>
{
    public Guid ReservationId { get; set; }
    public string AdminUserId { get; set; } = null!;
    public string? AdminNote { get; set; }

    public List<ReservationDocumentDto> Documents { get; set; } = new();
}
public class ReservationDocumentDto
{
    public string FileName { get; set; } = null!;
    public string Url { get; set; } = null!;
    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }
    public string? DocumentType { get; set; } // "Blueprint", etc.
}
