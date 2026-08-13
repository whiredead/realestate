namespace ProjectAPI.Api.Application.Reservations.UploadReservationDocument;

public class UploadReservationDocumentResponse
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}
