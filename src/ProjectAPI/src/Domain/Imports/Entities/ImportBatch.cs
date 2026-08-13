namespace ProjectAPI.Domain.Imports.Entities;

/// <summary>Excel stock import batch lifecycle (spec §5.11, §23).</summary>
public enum ImportBatchStatus
{
    Uploaded = 0,
    Validating = 1,
    ValidationFailed = 2,
    ReadyToCommit = 3,
    Committing = 4,
    Completed = 5,
    Failed = 6,
    Cancelled = 7
}

/// <summary>
/// One Excel stock-import attempt (Buildings + Units tabs — still no Floors
/// tab; a Unit row's free-text Floor column is resolved/created as a Floor
/// row per building at commit time, same name-matched pattern as Buildings).
///
/// The <see cref="FileHash"/> + <see cref="ProjectId"/>/row-count fingerprint
/// is what enforces §5.11's "commit refused if file/project/rows changed
/// since validation": commit re-validates the fingerprint before writing
/// anything, and a mismatch throws IMPORT_SOURCE_CHANGED rather than trusting
/// a stale validation result.
/// </summary>
public class ImportBatch
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public string FileName { get; set; } = string.Empty;

    /// <summary>SHA-256 of the uploaded file bytes.</summary>
    public string FileHash { get; set; } = string.Empty;

    public ImportBatchStatus Status { get; set; } = ImportBatchStatus.Uploaded;

    public int TotalRows { get; set; }
    public int ErrorRowCount { get; set; }

    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ValidatedAt { get; set; }
    public DateTime? CommittedAt { get; set; }

    /// <summary>Set on FAILED/CANCELLED for the audit trail.</summary>
    public string? FailureReason { get; set; }

    public ICollection<ImportRow> Rows { get; set; } = new List<ImportRow>();
}

/// <summary>
/// One row from either tab, with its validation outcome. Errors are recorded
/// per row rather than aborting the whole file on the first bad row, so the
/// admin sees the complete error report in one pass (§5.11).
/// </summary>
public class ImportRow
{
    public Guid Id { get; set; }

    public Guid BatchId { get; set; }
    public ImportBatch Batch { get; set; } = null!;

    /// <summary>"Buildings" or "Units" — which tab this row came from.</summary>
    public string Sheet { get; set; } = string.Empty;

    /// <summary>1-based row number in that sheet, for error messages the admin can act on.</summary>
    public int RowNumber { get; set; }

    /// <summary>Raw cell values, serialized, kept for audit even on a failed row.</summary>
    public string RawData { get; set; } = string.Empty;

    public bool IsValid { get; set; }

    /// <summary>Human-readable validation errors, empty when IsValid.</summary>
    public string? Errors { get; set; }

    /// <summary>Set once this row is actually written during commit.</summary>
    public bool Committed { get; set; }
}
