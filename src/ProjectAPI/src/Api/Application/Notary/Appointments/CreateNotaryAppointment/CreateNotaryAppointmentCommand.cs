namespace ProjectAPI.Api.Application.Notary.Appointments.CreateNotaryAppointment;

public class CreateNotaryAppointmentCommand : IRequest<CreateNotaryAppointmentResponse>
{
    public string? BuyerId { get; set; }
    public string? NotaireId { get; set; }
    public string? AgentId { get; set; }
    public string? ConnectedUserId { get; set; }
    public Guid ReservationId { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string Status { get; set; } = "Scheduled";
    public decimal TaxFees { get; set; }
    public decimal TahfidFees { get; set; }
}

