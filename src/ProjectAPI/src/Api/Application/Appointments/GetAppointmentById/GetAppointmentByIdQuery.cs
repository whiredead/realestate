using ProjectAPI.Api.Application.Common.Models;

namespace ProjectAPI.Api.Application.Appointments.GetAppointmentById;

public class GetAppointmentByIdQuery : IRequest<AppointmentResponse>
{
    public Guid AppointmentId { get; set; }
}
