using ProjectAPI.Api.Application.Common.Idempotency;

namespace ProjectAPI.Api.Application.Reservations.ApproveReservation;

/// <summary>§7 — approval requires an Idempotency-Key: a retried approve must not double-grant.</summary>
public class ApproveReservationCommand : IRequest<bool>, IIdempotentRequest
{
    public Guid ReservationId { get; set; }
    public string AdminUserId { get; set; } = null!;
    public string? AdminNote { get; set; }
    public string? IdempotencyKey { get; set; }

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
