namespace ProjectAPI.Api.Application.Reservations.UploadReservationDocument;

/// <summary>
/// Uploads a file to blob storage and persists it as a ReservationDocument
/// row — closing the gap where ReservationDocument was read (GetReservationByIdHandler
/// projects reservation.Documents already) but nothing ever wrote one.
/// </summary>
public class UploadReservationDocumentCommand : IRequest<UploadReservationDocumentResponse>
{
    public Guid ReservationId { get; set; }
    public IFormFile File { get; set; } = null!;

    /// <summary>Free-text category — e.g. "Contract", "CIN", "Blueprint" (matches the entity's existing convention).</summary>
    public string? DocumentType { get; set; }
}
