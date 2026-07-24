using ProjectAPI.Api.Application.Common.Models;

namespace ProjectAPI.Api.Application.Notary.Appointments.GetNotaryAppointments;

public class GetNotaryAppointmentsQuery : IRequest<PaginatedResponse<NotaryAppointmentResponse>>
{
    public Guid? ReservationId { get; set; }
    public string? BuyerCIN { get; set; }
    public string? NotaireId { get; set; }
    public string? AgentId { get; set; }
    public string? Status { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

