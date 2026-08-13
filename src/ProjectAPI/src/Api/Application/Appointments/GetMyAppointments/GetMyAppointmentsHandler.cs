using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Appointments.GetMyAppointments;

public class GetMyAppointmentsHandler : IRequestHandler<GetMyAppointmentsQuery, List<MyAppointmentSummary>>
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public GetMyAppointmentsHandler(ApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<MyAppointmentSummary>> Handle(GetMyAppointmentsQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrWhiteSpace(userId)) return new List<MyAppointmentSummary>();

        return await _db.Set<Appointment>()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.AppointmentDate)
            .Select(a => new MyAppointmentSummary
            {
                Id = a.Id,
                ProjectId = a.ProjectId,
                SalesAgentId = a.SalesAgentId,
                AppointmentDate = a.AppointmentDate,
                PropertyType = a.PropertyType,
                Status = a.Status,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(ct);
    }
}
