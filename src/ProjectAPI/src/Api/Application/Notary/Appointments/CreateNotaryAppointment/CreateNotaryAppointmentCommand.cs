using ProjectAPI.Api.Application.Common.Idempotency;

namespace ProjectAPI.Api.Application.Notary.Appointments.CreateNotaryAppointment;

/// <summary>§7 — requires an Idempotency-Key: a retried request must not create two notary appointments for the same reservation.</summary>
public class CreateNotaryAppointmentCommand : IRequest<CreateNotaryAppointmentResponse>, IIdempotentRequest
{
    public string? IdempotencyKey { get; set; }
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

