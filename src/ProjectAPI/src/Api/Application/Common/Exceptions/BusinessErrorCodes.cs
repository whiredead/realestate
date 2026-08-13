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

    // --- SAV / warranty --------------------------------------------------------

    /// <summary>A SAV claim was opened against a unit that is not yet DELIVERED (§5.9).</summary>
    public const string PropertyNotDelivered = "PROPERTY_NOT_DELIVERED";

    /// <summary>A SAV claim was opened outside any active warranty period (§5.9).</summary>
    public const string WarrantyExpired = "WARRANTY_EXPIRED";

    // --- Excel stock import ----------------------------------------------------

    /// <summary>
    /// §5.11 — the file, project or rows changed between validation and commit.
    /// The commit re-checks the batch's fingerprint (file hash + row count)
    /// before writing anything rather than trusting a stale validation result.
    /// </summary>
    public const string ImportSourceChanged = "IMPORT_SOURCE_CHANGED";

    // --- Generic -------------------------------------------------------------

    /// <summary>Input failed validation (field-level errors are in <c>errors</c>).</summary>
    public const string ValidationFailed = "VALIDATION_FAILED";

    /// <summary>The resource does not exist, or is outside the caller's scope.</summary>
    public const string NotFound = "NOT_FOUND";

    /// <summary>The caller is not authenticated.</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>
    /// The resource belongs to a project outside the caller's assigned
    /// perimeter (§6.4). Distinct from <see cref="Unauthorized"/>: the caller
    /// is authenticated and correctly roled, just not assigned here.
    /// </summary>
    public const string ProjectScopeDenied = "PROJECT_SCOPE_DENIED";

    /// <summary>
    /// §6.4 — the caller is the reservation's owning sales agent and may not
    /// approve their own reservation. Authenticated and roled, but conflicted.
    /// </summary>
    public const string SelfApprovalForbidden = "SELF_APPROVAL_FORBIDDEN";

    /// <summary>An unexpected server-side failure.</summary>
    public const string InternalError = "INTERNAL_ERROR";

    // --- Internal invitations (Phase 2) --------------------------------------

    /// <summary>No invitation matches the supplied token.</summary>
    public const string InvitationNotFound = "INVITATION_NOT_FOUND";

    /// <summary>The invitation was revoked before being accepted.</summary>
    public const string InvitationRevoked = "INVITATION_REVOKED";

    /// <summary>The invitation has already been accepted once.</summary>
    public const string InvitationAlreadyAccepted = "INVITATION_ALREADY_ACCEPTED";

    /// <summary>The invitation's validity window has passed.</summary>
    public const string InvitationExpired = "INVITATION_EXPIRED";

    /// <summary>The caller may not invite this role (permission matrix, §6.3).</summary>
    public const string InvitationRoleForbidden = "INVITATION_ROLE_FORBIDDEN";

    /// <summary>AuthenticationAPI could not be reached or failed to provision the account.</summary>
    public const string InvitationProvisioningFailed = "INVITATION_PROVISIONING_FAILED";

    /// <summary>The chosen agent has no active SALES_AGENT ProjectMembership for the project.</summary>
    public const string AgentNotEligibleForProject = "AGENT_NOT_ELIGIBLE_FOR_PROJECT";

    /// <summary>No eligible agent could be found to auto-assign this appointment.</summary>
    public const string NoEligibleAgentFound = "NO_ELIGIBLE_AGENT_FOUND";
}
