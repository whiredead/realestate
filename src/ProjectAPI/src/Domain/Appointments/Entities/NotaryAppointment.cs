using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Domain.Appointments.Entities;

/// <summary>
/// Represents an appointment with a notary.
/// </summary>
public class NotaryAppointment
{
    public Guid Id { get; set; }
    public string? BuyerId { get; set; }
    public string? NotaireId { get; set; }
    public string? AgentId { get; set; }
    public string? ConnectedUserId { get; set; } // The ID of the user in the system making the appointment
    public Guid ReservationId { get; set; } // The reservation associated with this appointment
    public DateTime AppointmentDate { get; set; }

    /// <summary>
    /// Lifecycle of the meeting itself, on the shared appointment machine (§5.1).
    /// Says nothing about what the meeting decided — see <see cref="Outcome"/>.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// What the appointment decided (§5.7). Null until the appointment completes;
    /// completing one without an outcome is refused, because the outcome — not the
    /// status — is what converts the sale. Only PURCHASE_COMPLETED does anything.
    /// </summary>
    public NotaryAppointmentOutcome? Outcome { get; set; }

    public DateTime? OutcomeRecordedAt { get; set; }

    /// <summary>Taken from the token, not the request body.</summary>
    public string? OutcomeRecordedBy { get; set; }

    /// <summary>Free-text note explaining a non-completing outcome.</summary>
    public string? OutcomeNote { get; set; }

    // Reservation Info for disconnected users
    public string? BuyerFirstName { get; set; }
    public string? BuyerLastName { get; set; }
    public string? BuyerCIN { get; set; }
    public string? BuyerEmail { get; set; }
    public string? BuyerPhoneNumber { get; set; }

    // Financial details
    public decimal PropertyPrice { get; set; } // The total property price
    public decimal TaxFees { get; set; } // Tax fees
    public decimal TahfidFees { get; set; } // Tahfid fees for property registration

    public Reservation Reservation { get; set; } // Navigation property to Reservation
    public Notary Notary { get; set; }
}
