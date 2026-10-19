namespace ProjectAPI.Domain.Reservations.Entities;

/// <summary>
/// Contract step of an APPROVED reservation, tracked beside
/// <see cref="ReservationStatus"/> rather than as new lifecycle states: every
/// rule that keys on Approved (unit hold, buyer access, payments, final visit)
/// keeps holding while the contract is being signed.
///
/// Persisted as int; values must not be renumbered.
/// </summary>
public enum ReservationContractStatus
{
    /// <summary>No contract filed yet.</summary>
    None = 0,

    /// <summary>Contract filed by the sales administrator, awaiting the client's signature on paper.</summary>
    Issued = 1,

    /// <summary>Scan of the signed contract filed. Prerequisite of a notary appointment.</summary>
    Signed = 2
}

/// <summary>
/// Document types written only by the contract endpoints. The generic
/// document upload refuses them, so a signed contract on file always went
/// through <c>POST {id}/contract/signed</c>.
/// </summary>
public static class ReservationContractDocumentTypes
{
    public const string Contract = "SALES_CONTRACT";
    public const string SignedContract = "SALES_CONTRACT_SIGNED";

    public static bool IsReserved(string? documentType) =>
        string.Equals(documentType?.Trim(), Contract, StringComparison.OrdinalIgnoreCase)
        || string.Equals(documentType?.Trim(), SignedContract, StringComparison.OrdinalIgnoreCase);
}
