namespace ProjectAPI.Domain.Payments.Entities;

/// <summary>
/// Evidence of a received payment (bank transfer receipt, cheque photo, cash
/// voucher…), uploaded before the payment is recorded and then attached to
/// exactly one <see cref="Payment"/> through <see cref="Payment.ProofId"/>.
///
/// Lives in its own private blob container, apart from the reservation dossier,
/// and is never deleted: it is part of the financial trail.
/// </summary>
public class PaymentProof
{
    public Guid Id { get; set; }

    /// <summary>The file the proof was uploaded for; a proof can only back a payment on this reservation.</summary>
    public Guid ReservationId { get; set; }

    /// <summary>Name of the blob inside the payment-proofs container (never returned to clients).</summary>
    public string BlobName { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string? UploadedBy { get; set; }
}
