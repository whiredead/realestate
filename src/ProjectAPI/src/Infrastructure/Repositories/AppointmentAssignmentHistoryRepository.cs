using Als.Foundation.Data.EntityFramework;
using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.Appointments.Interfaces;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Infrastructure.Repositories;

public class AppointmentAssignmentHistoryRepository : BaseRepository<AppointmentAssignmentHistory>, IAppointmentAssignmentHistoryRepository
{
    public AppointmentAssignmentHistoryRepository(ApplicationDbContext context) : base(context)
    {
    }
}
