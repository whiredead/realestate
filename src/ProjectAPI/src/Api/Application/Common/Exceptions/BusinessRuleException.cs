namespace ProjectAPI.Api.Application.Common.Exceptions;

/// <summary>
/// Raised when a business rule rejects a command. Carries a stable
/// <see cref="Code"/> from <see cref="BusinessErrorCodes"/> and the HTTP status
/// the API contract associates with it (spec §31.3, §31.7).
///
/// Controllers never construct these: they are thrown by the domain/handlers and
/// translated by <c>ApiExceptionFilter</c>.
/// </summary>
public class BusinessRuleException : Exception
{
    /// <summary>Stable machine-readable code (see <see cref="BusinessErrorCodes"/>).</summary>
    public string Code { get; }

    /// <summary>HTTP status to return. 409 for conflicts, 422 for rule violations.</summary>
    public int StatusCode { get; }

    public BusinessRuleException(string code, string message, int statusCode = StatusCodes.Status422UnprocessableEntity)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    // --- Factories for the cases the spec names explicitly --------------------

    /// <summary>§7.7 — the unit is no longer selectable.</summary>
    public static BusinessRuleException UnitNotAvailable(Guid unitId) =>
        new(BusinessErrorCodes.UnitNotAvailable,
            $"Le bien {unitId} n'est plus disponible.",
            StatusCodes.Status409Conflict);

    /// <summary>§10.1 — overlapping appointment for the same agent or notary.</summary>
    public static BusinessRuleException AppointmentSlotConflict(DateTime startsAt) =>
        new(BusinessErrorCodes.AppointmentSlotConflict,
            $"Le créneau du {startsAt:dd/MM/yyyy HH:mm} n'est plus disponible.",
            StatusCodes.Status409Conflict);

    /// <summary>Transition refused by the state machine.</summary>
    public static BusinessRuleException InvalidStatusTransition(string from, string to) =>
        new(BusinessErrorCodes.InvalidStatusTransition,
            $"Transition non autorisée : {from} → {to}.",
            StatusCodes.Status409Conflict);

    /// <summary>§31.6 — optimistic concurrency failure.</summary>
    public static BusinessRuleException ResourceVersionConflict() =>
        new(BusinessErrorCodes.ResourceVersionConflict,
            "La ressource a été modifiée entre-temps. Rechargez puis réessayez.",
            StatusCodes.Status409Conflict);

    /// <summary>§12.1 — a mandatory document is absent.</summary>
    public static BusinessRuleException MissingRequiredDocument(string documentType) =>
        new(BusinessErrorCodes.MissingRequiredDocument,
            $"Document obligatoire manquant : {documentType}.");

    /// <summary>§12.1 — discount above the agent's ceiling.</summary>
    public static BusinessRuleException DiscountLimitExceeded(decimal requested, decimal ceiling) =>
        new(BusinessErrorCodes.DiscountLimitExceeded,
            $"Remise demandée ({requested:N2}) supérieure au plafond autorisé ({ceiling:N2}).");

    // --- Sale (§5.7, §6) ------------------------------------------------------

    /// <summary>
    /// §6 — a reservation carries at most one active sale. The read-then-write
    /// check in the handler is racy on its own; the filtered unique index
    /// IX_Sales_ActivePerReservation catches the loser and ApiExceptionFilter
    /// turns it into this same code, so the client sees one behaviour either way.
    /// </summary>
    public static BusinessRuleException SaleAlreadyExists() =>
        new(BusinessErrorCodes.SaleAlreadyExists,
            "Une vente active existe déjà pour cette réservation.",
            StatusCodes.Status409Conflict);

    /// <summary>
    /// §6 — only Draft and PendingNotary accept edits. A Confirmed sale is the
    /// record of a notarial act and a Cancelled one is terminal; neither is
    /// rewritten, they are superseded.
    /// </summary>
    public static BusinessRuleException SaleNotEditable(string status) =>
        new(BusinessErrorCodes.SaleNotEditable,
            $"Cette vente n'est plus modifiable (statut : {status}).",
            StatusCodes.Status409Conflict);

    /// <summary>§18.3 FR-NOT-002 — dossier not eligible for a notary appointment.</summary>
    public static BusinessRuleException NotaryNotEligible(IEnumerable<string> reasons) =>
        new(BusinessErrorCodes.NotaryNotEligible,
            "Le dossier n'est pas éligible au rendez-vous notarial : " + string.Join(" ", reasons),
            StatusCodes.Status422UnprocessableEntity);

    /// <summary>§5.9 — a SAV claim was opened against a unit that is not yet DELIVERED.</summary>
    public static BusinessRuleException PropertyNotDelivered(Guid unitId) =>
        new(BusinessErrorCodes.PropertyNotDelivered,
            $"Le bien {unitId} n'a pas encore été livré ; le SAV n'est pas ouvert.",
            StatusCodes.Status409Conflict);

    /// <summary>§5.9 — no active warranty covers this unit at the time of the claim.</summary>
    public static BusinessRuleException WarrantyExpired(Guid unitId) =>
        new(BusinessErrorCodes.WarrantyExpired,
            $"Aucune garantie active ne couvre le bien {unitId}.",
            StatusCodes.Status409Conflict);

    /// <summary>§5.11 — the file/project/rows changed since the batch was validated.</summary>
    public static BusinessRuleException ImportSourceChanged() =>
        new(BusinessErrorCodes.ImportSourceChanged,
            "Le fichier ou les données ont changé depuis la validation. Revalidez avant de confirmer l'import.",
            StatusCodes.Status409Conflict);

    // --- Project perimeter (§6.4) --------------------------------------------

    /// <summary>
    /// §6.4 — the resource belongs to a project the caller is not assigned to.
    ///
    /// The message deliberately does not name the project: the caller has just
    /// been told they may not see it, so echoing its identifier back would
    /// confirm its existence to someone outside the perimeter. The id is carried
    /// as a parameter for logging/telemetry at the throw site, not for the user.
    /// </summary>
    public static BusinessRuleException ProjectScopeDenied(Guid projectId) =>
        new(BusinessErrorCodes.ProjectScopeDenied,
            "Cette ressource n'appartient pas à votre périmètre de projets.",
            StatusCodes.Status403Forbidden);

    /// <summary>
    /// §6.4 — "Un acheteur ne peut accéder qu'à ses propres données". Same code
    /// and status as <see cref="ProjectScopeDenied"/>, but a buyer has no project
    /// perimeter, so the perimeter wording would be meaningless to them.
    /// </summary>
    public static BusinessRuleException BuyerScopeDenied() =>
        new(BusinessErrorCodes.ProjectScopeDenied,
            "Vous n'avez pas accès à ce dossier.",
            StatusCodes.Status403Forbidden);

    /// <summary>
    /// §6.3 — a NOTARY manages only their own weekly availability and blocks;
    /// admins oversee. The route's {notaryId} segment must match the caller's
    /// own id for a NOTARY caller, never an arbitrary target.
    /// </summary>
    public static BusinessRuleException NotaryCalendarScopeDenied() =>
        new(BusinessErrorCodes.ProjectScopeDenied,
            "Vous ne pouvez gérer que votre propre calendrier.",
            StatusCodes.Status403Forbidden);

    /// <summary>
    /// §6.3 — a SALES_AGENT manages only their own availability calendar
    /// (weekly hours, blocks, date overrides, appointment settings); admins
    /// oversee. Mirrors <see cref="NotaryCalendarScopeDenied"/>.
    /// </summary>
    public static BusinessRuleException AgentCalendarScopeDenied() =>
        new(BusinessErrorCodes.ProjectScopeDenied,
            "Vous ne pouvez gérer que votre propre calendrier.",
            StatusCodes.Status403Forbidden);

    /// <summary>
    /// §6.4 — "Un agent commercial ne peut pas approuver sa propre réservation."
    /// A separation-of-duties rule: the owning agent, even with admin rights,
    /// may not be the one who validates the dossier they submitted.
    /// </summary>
    public static BusinessRuleException SelfApprovalForbidden() =>
        new(BusinessErrorCodes.SelfApprovalForbidden,
            "Vous ne pouvez pas approuver une réservation que vous avez soumise.",
            StatusCodes.Status403Forbidden);

    // --- Internal invitations (Phase 2) --------------------------------------

    /// <summary>
    /// Deliberately vague — do not confirm or deny that a token ever existed,
    /// same reasoning as <see cref="ProjectScopeDenied"/> not naming the project.
    /// </summary>
    public static BusinessRuleException InvitationNotFound() =>
        new(BusinessErrorCodes.InvitationNotFound,
            "Ce lien d'invitation n'est pas valide.",
            StatusCodes.Status404NotFound);

    public static BusinessRuleException InvitationRevoked() =>
        new(BusinessErrorCodes.InvitationRevoked,
            "Cette invitation a été révoquée. Contactez votre administrateur pour en recevoir une nouvelle.",
            StatusCodes.Status409Conflict);

    public static BusinessRuleException InvitationAlreadyAccepted() =>
        new(BusinessErrorCodes.InvitationAlreadyAccepted,
            "Cette invitation a déjà été utilisée.",
            StatusCodes.Status409Conflict);

    public static BusinessRuleException InvitationExpired() =>
        new(BusinessErrorCodes.InvitationExpired,
            "Cette invitation a expiré. Demandez à votre administrateur d'en envoyer une nouvelle.",
            StatusCodes.Status409Conflict);

    /// <summary>
    /// §6.3 permission matrix — a PROJECT_ADMIN may invite SALES_AGENT,
    /// TECHNICIAN, or NOTARY only; inviting PROJECT_ADMIN or GLOBAL_ADMIN
    /// requires GLOBAL_ADMIN.
    /// </summary>
    public static BusinessRuleException InvitationRoleForbidden(string roleCode) =>
        new(BusinessErrorCodes.InvitationRoleForbidden,
            $"Vous n'êtes pas autorisé à inviter le rôle {roleCode}.",
            StatusCodes.Status403Forbidden);

    /// <summary>
    /// The AuthenticationAPI call (account creation/role grant) failed or
    /// could not be reached. Nothing has been written on either side yet
    /// (see AcceptInternalInvitationHandler) — safe to retry.
    /// </summary>
    public static BusinessRuleException InvitationProvisioningFailed() =>
        new(BusinessErrorCodes.InvitationProvisioningFailed,
            "La création du compte a échoué. Réessayez dans quelques instants.",
            StatusCodes.Status503ServiceUnavailable);
}
