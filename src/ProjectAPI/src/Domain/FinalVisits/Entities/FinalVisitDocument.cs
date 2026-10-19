namespace ProjectAPI.Domain.FinalVisits.Entities;

/// <summary>
/// A file attached to a final-visit case: photos of the visit or of a snag,
/// the photo proving a snag's repair, or the scan of the visit report (PV)
/// signed on paper by the buyer. Stored in a private blob container and
/// never deleted — it is part of the delivery trail.
/// </summary>
public class FinalVisitDocument
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }

    /// <summary>Set for REPORT_PHOTO (the report version the photo belongs to).</summary>
    public Guid? ReportId { get; set; }

    /// <summary>Set for SNAG_PHOTO and SNAG_PROOF.</summary>
    public Guid? SnagId { get; set; }

    /// <summary>One of <see cref="FinalVisitDocumentKinds"/>.</summary>
    public string Kind { get; set; } = FinalVisitDocumentKinds.ReportPhoto;

    public string BlobName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string? UploadedBy { get; set; }
}

public static class FinalVisitDocumentKinds
{
    /// <summary>Photo taken during the visit, attached to the report.</summary>
    public const string ReportPhoto = "REPORT_PHOTO";

    /// <summary>Photo of a snag as found.</summary>
    public const string SnagPhoto = "SNAG_PHOTO";

    /// <summary>Photo proving a snag was repaired. Required to resolve a major or blocking snag.</summary>
    public const string SnagProof = "SNAG_PROOF";

    /// <summary>Scan of the visit report signed by the buyer. Required before the notary appointment.</summary>
    public const string SignedPv = "SIGNED_PV";

    public static readonly string[] All = { ReportPhoto, SnagPhoto, SnagProof, SignedPv };
}
