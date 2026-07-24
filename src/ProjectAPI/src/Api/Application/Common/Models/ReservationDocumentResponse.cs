namespace ProjectAPI.Api.Application.Common.Models;

public class ReservationDocumentResponse
{
    public Guid Id { get; set; }
    public string? Name { get; set; }     // adjust to your entity (e.g., FileName)
    public string? Url { get; set; }      // adjust to your entity (e.g., Path)
    public DateTime UploadedAt { get; set; } // adjust if your entity uses another field
    public string? UploadedBy { get; set; }  // optional: include if you track this
}
