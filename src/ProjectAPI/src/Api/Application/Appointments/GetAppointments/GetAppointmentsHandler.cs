using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.Appointments.Interfaces;
using System.Linq.Expressions;

namespace ProjectAPI.Api.Application.Appointments.GetAppointments;

/// <summary>
/// Handler for getting the list of appointments based on filters.
/// </summary>
/// <remarks>
/// This handler processes the <see cref="GetAppointmentsQuery" /> to retrieve a list of appointments that match the specified filters.
/// It queries the appointment repository with the given filtering criteria and includes related entities such as Project and Agent.
/// </remarks>
public class GetAppointmentsHandler : IRequestHandler<GetAppointmentsQuery, PaginatedResponse<AppointmentResponse>>
{
    private readonly IAppointmentRepository _appointmentRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetAppointmentsHandler" /> class.
    /// </summary>
    /// <param name="appointmentRepository">The repository to access appointment data.</param>
    public GetAppointmentsHandler(IAppointmentRepository appointmentRepository)
    {
        _appointmentRepository = appointmentRepository;
    }

    /// <summary>
    /// Handles the query to get a list of appointments based on filters.
    /// </summary>
    /// <param name="request">The <see cref="GetAppointmentsQuery" /> containing filtering criteria such as ProjectId, AgentId, AppointmentDate, etc.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation, containing a list of <see cref="AppointmentResponse" />.</returns>
    public async Task<PaginatedResponse<AppointmentResponse>> Handle(GetAppointmentsQuery request, CancellationToken cancellationToken)
    {
        Expression<Func<Appointment, bool>> filter = null;

        var includes = new Expression<Func<Appointment, object>>[]
        {
                a=>a.Project,
                a => a.Agent
        };

        var appointments = await _appointmentRepository.Find(a =>
                  (!request.ProjectId.HasValue || a.ProjectId == request.ProjectId.Value) &&
                  (string.IsNullOrEmpty(request.AgentId) || a.AgentId == request.AgentId) &&
                  (!request.AppointmentDate.HasValue || a.AppointmentDate.Date == request.AppointmentDate.Value.Date) &&
                  (string.IsNullOrEmpty(request.UserId) || a.UserId == request.UserId) &&
                  (string.IsNullOrEmpty(request.Status) || a.Status == request.Status) &&
                  (string.IsNullOrEmpty(request.Name) || a.Name.Contains(request.Name)) &&
                  (string.IsNullOrEmpty(request.LastName) || a.LastName.Contains(request.LastName)) &&
                  (string.IsNullOrEmpty(request.Email) || a.Email.Contains(request.Email)) &&
                  (string.IsNullOrEmpty(request.PhoneNumber) || a.PhoneNumber.Contains(request.PhoneNumber)), includes);

       
        var totalItems = appointments.Count();
        var paginatedData = appointments
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new AppointmentResponse
            {
                Id = a.Id,
                ProjectId = a.ProjectId,
                TypeBienIds = a.TypeBienIds,
                AgentId = a.AgentId,
                PropertyType = a.PropertyType,
                AgentFullName = a.Agent.FirstName + " " + a.Agent.LastName,
                ProjectName = a.Project.Name,
                AppointmentDate = a.AppointmentDate,
                UserId = a.UserId != null ? Guid.Parse(a.UserId) : null,
                Status = a.Status,
                Name = a.Name,
                LastName = a.LastName,
                Email = a.Email,
                PhoneNumber = a.PhoneNumber
            }).ToList();

        return new PaginatedResponse<AppointmentResponse>(paginatedData, request.PageNumber, request.PageSize, totalItems);
    }
}