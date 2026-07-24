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
    public string Status { get; set; } // e.g., Scheduled, Completed, Cancelled

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
