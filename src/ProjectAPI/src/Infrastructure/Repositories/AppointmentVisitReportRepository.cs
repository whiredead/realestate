using Als.Foundation.Data.EntityFramework;
using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.Appointments.Interfaces;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Infrastructure.Repositories;

public class AppointmentVisitReportRepository : BaseRepository<AppointmentVisitReport>, IAppointmentVisitReportRepository
{
    public AppointmentVisitReportRepository(ApplicationDbContext context) : base(context)
    {
    }
}
