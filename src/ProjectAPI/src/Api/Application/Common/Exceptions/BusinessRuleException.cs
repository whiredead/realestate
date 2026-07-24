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

    /// <summary>§18.3 FR-NOT-002 — dossier not eligible for a notary appointment.</summary>
    public static BusinessRuleException NotaryNotEligible(IEnumerable<string> reasons) =>
        new(BusinessErrorCodes.NotaryNotEligible,
            "Le dossier n'est pas éligible au rendez-vous notarial : " + string.Join(" ", reasons),
            StatusCodes.Status422UnprocessableEntity);
}
