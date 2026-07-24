namespace ProjectAPI.Api.Application.Common.Exceptions;

/// <summary>
/// Stable business error codes returned in the <c>code</c> member of a
/// <c>application/problem+json</c> response (spec §31.7).
///
/// These values are part of the public API contract: clients branch on them.
/// They MUST NOT be renamed or repurposed — add a new code instead.
/// </summary>
public static class BusinessErrorCodes
{
    // --- Concurrency / availability -----------------------------------------

    /// <summary>The unit is already held, reserved or otherwise not selectable.</summary>
    public const string UnitNotAvailable = "UNIT_NOT_AVAILABLE";

    /// <summary>An appointment overlaps an existing one for the same agent or notary.</summary>
    public const string AppointmentSlotConflict = "APPOINTMENT_SLOT_CONFLICT";

    /// <summary>The caller supplied a stale version (If-Match / ETag mismatch).</summary>
    public const string ResourceVersionConflict = "RESOURCE_VERSION_CONFLICT";

    /// <summary>The same idempotency key was reused with a different payload.</summary>
    public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";

    // --- State machine -------------------------------------------------------

    /// <summary>The requested transition is not permitted from the current status.</summary>
    public const string InvalidStatusTransition = "INVALID_STATUS_TRANSITION";

    // --- Reservation ---------------------------------------------------------

    /// <summary>A document required by the project configuration is missing.</summary>
    public const string MissingRequiredDocument = "MISSING_REQUIRED_DOCUMENT";

    /// <summary>The requested discount exceeds the agent's authorised ceiling.</summary>
    public const string DiscountLimitExceeded = "DISCOUNT_LIMIT_EXCEEDED";

    // --- Notary / final visit -------------------------------------------------

    /// <summary>The dossier is not eligible for a notary appointment (§17.6).</summary>
    public const string NotaryNotEligible = "NOTARY_NOT_ELIGIBLE";

    // --- Generic -------------------------------------------------------------

    /// <summary>Input failed validation (field-level errors are in <c>errors</c>).</summary>
    public const string ValidationFailed = "VALIDATION_FAILED";

    /// <summary>The resource does not exist, or is outside the caller's scope.</summary>
    public const string NotFound = "NOT_FOUND";

    /// <summary>The caller is not authenticated.</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>An unexpected server-side failure.</summary>
    public const string InternalError = "INTERNAL_ERROR";
}
