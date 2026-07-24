namespace ProjectAPI.Api.Application.Reservations.ResubmitReservation;

/// <summary>
/// Agent returns a corrected reservation for a new administrative decision
/// (spec §12.2 / §12.4: CHANGES_REQUESTED → SUBMITTED, DRAFT → SUBMITTED).
/// </summary>
public class ResubmitReservationCommand : IRequest<bool>
{
    /// <summary>Reservation being resubmitted. Bound from the route.</summary>
    public Guid ReservationId { get; set; }

    /// <summary>Optional note describing what the agent corrected.</summary>
    public string? AgentNote { get; set; }
}
