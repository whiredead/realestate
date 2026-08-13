namespace ProjectAPI.Api.Application.Notary.Appointments.GetNotaryAppointments;

public class NotaryAppointmentResponse
{
    public Guid Id { get; set; }
    public string? BuyerId { get; set; }
    public string? NotaireId { get; set; }
    public string? NotaireFullName { get; set; }
    public string? AgentId { get; set; }
    public Guid ReservationId { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string Status { get; set; }
    public string? BuyerFirstName { get; set; }
    public string? BuyerLastName { get; set; }
    public string? BuyerCIN { get; set; }
    public string? BuyerEmail { get; set; }
    public string? BuyerPhoneNumber { get; set; }
    public decimal PropertyPrice { get; set; }
    public decimal TaxFees { get; set; }
    public decimal TahfidFees { get; set; }

    /// <summary>§5.7 — the outcome recorded when the appointment was completed; the projection previously omitted this, so the "Résultat" column always read "—" regardless of what was recorded (N17).</summary>
    public string? Outcome { get; set; }
    public string? OutcomeNote { get; set; }
}

