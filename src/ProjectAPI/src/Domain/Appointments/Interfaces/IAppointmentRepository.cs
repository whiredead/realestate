using Als.Foundation.Data.Abstractions.EntityFramework;
using ProjectAPI.Domain.Appointments.Entities;

namespace ProjectAPI.Domain.Appointments.Interfaces;
/// <summary>
/// Interface for Appointment repository operations.
/// </summary>
public interface IAppointmentRepository : IBaseRepository<Appointment>
{
}

