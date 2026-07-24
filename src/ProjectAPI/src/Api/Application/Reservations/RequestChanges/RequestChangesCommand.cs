namespace ProjectAPI.Api.Application.Reservations.RequestChanges;

/// <summary>
/// Asks the submitting agent to correct a reservation (spec §12.2, FR-RES-007).
/// The unit stays blocked while the correction is pending.
/// </summary>
public class RequestChangesCommand : IRequest<bool>
{
    /// <summary>Reservation to send back for correction. Bound from the route.</summary>
    public Guid ReservationId { get; set; }

    /// <summary>Administrator asking for the correction.</summary>
    public string AdminUserId { get; set; } = string.Empty;

    /// <summary>What must be corrected. Mandatory — the agent needs actionable feedback.</summary>
    public string Reason { get; set; } = string.Empty;
}
