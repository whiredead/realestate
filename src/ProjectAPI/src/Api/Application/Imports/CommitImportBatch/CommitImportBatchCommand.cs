using ProjectAPI.Api.Application.Common.Idempotency;

namespace ProjectAPI.Api.Application.Imports.CommitImportBatch;

/// <summary>§7 — commit requires an Idempotency-Key: a retried commit must not double-write the stock.</summary>
public class CommitImportBatchCommand : IRequest<CommitImportBatchResponse>, IIdempotentRequest
{
    public string? IdempotencyKey { get; set; }
    public Guid BatchId { get; set; }

    /// <summary>
    /// Same file bytes the caller validated with. Re-sent (rather than reusing
    /// the stored copy) so a change to the underlying file between validate
    /// and commit is caught by the hash comparison, not silently missed.
    /// </summary>
    public byte[] FileContent { get; set; } = Array.Empty<byte>();

    public string? CommittedBy { get; set; }
}

public class CommitImportBatchResponse
{
    public Guid BatchId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int BuildingsCreated { get; set; }
    public int FloorsCreated { get; set; }
    public int UnitsCreated { get; set; }
    public int UnitsUpdated { get; set; }
    public string Message { get; set; } = string.Empty;
}
