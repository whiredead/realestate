namespace ProjectAPI.Api.Application.Reservations.CreateReservation;
public class CreateReservationResponse
{
    public Guid ReservationId { get; set; }
    public string Message { get; set; } = "Reservation created (Pending).";
    public bool Success { get; set; } = true;
    public string Details { get; set; }

    /// <summary>
    /// Set when the buyer has no account yet: the token of the link they use to choose their own password.
    /// The agent shares it; the web app builds the /activate?token=… address from it.
    /// </summary>
    public string? ActivationToken { get; set; }
    public DateTime? ActivationExpiresAt { get; set; }
}
