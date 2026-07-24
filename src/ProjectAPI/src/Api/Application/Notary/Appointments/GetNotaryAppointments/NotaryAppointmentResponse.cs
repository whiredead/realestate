namespace ProjectAPI.Api.Application.Notary.Appointments.GetNotaryAppointments;

public class NotaryAppointmentResponse
{
    public Guid Id { get; set; }
    public string? BuyerId { get; set; }
    public string? NotaireId { get; set; }
    public string? AgentId { get; set; }
    public Guid ReservationId { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string Status { get; set; }
    public decimal PropertyPrice { get; set; }
    public decimal TaxFees { get; set; }
    public decimal TahfidFees { get; set; }
}

