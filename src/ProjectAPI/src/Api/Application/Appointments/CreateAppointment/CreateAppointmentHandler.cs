using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.Appointments.Interfaces;
using ProjectAPI.Api.Application.Common.Exceptions;
using Microsoft.AspNetCore.Identity;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Domain.Users.Interfaces;

namespace ProjectAPI.Api.Application.Appointments.CreateAppointment;

/// <summary>
/// Handles the creation of a new appointment.
/// </summary>
public class CreateAppointmentHandler : IRequestHandler<CreateAppointmentCommand, CreateAppointmentResponse>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly UserManager<User> _userManager;
    private readonly IPerformanceIndicatorRepository _performanceIndicatorRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateAppointmentHandler"/> class.
    /// </summary>
    /// <param name="appointmentRepository">The repository for managing appointments.</param>
    /// <param name="userManager">The user manager for managing user information.</param>
    /// <param name="performanceIndicatorRepository">The repository for managing performance indicators.</param>
    public CreateAppointmentHandler(
        IAppointmentRepository appointmentRepository,
        UserManager<User> userManager,
        IPerformanceIndicatorRepository performanceIndicatorRepository)
    {
        _appointmentRepository = appointmentRepository;
        _userManager = userManager;
        _performanceIndicatorRepository = performanceIndicatorRepository;
    }

    /// <summary>
    /// Handles the creation of a new appointment based on the provided command.
    /// </summary>
    /// <param name="request">The command containing appointment creation details.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A response containing the appointment ID and a success message.</returns>
    public async Task<CreateAppointmentResponse> Handle(CreateAppointmentCommand request, CancellationToken cancellationToken)
    {
        // Fill user information if UserId is provided and user is authenticated
        if (!string.IsNullOrEmpty(request.UserId))
        {
            var user = await _userManager.FindByIdAsync(request.UserId) ?? throw new NotFoundException("User not found.");
            request.Name = user.FirstName;
            request.LastName = user.LastName;
            request.Email = user.Email;
            request.PhoneNumber = user.PhoneNumber;
        }

        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            AgentId = request.AgentId.HasValue ? request.AgentId.Value.ToString() : null,
            AppointmentDate = request.AppointmentDate,
            TypeBienIds = request.TypeBienIds,
            PropertyType = request.PropertyType,
            UserId = string.IsNullOrEmpty(request.UserId) ? null : request.UserId,
            Name = request.Name,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            LastName = request.LastName,
            Status = "En cours de traitement",
        };

        await _appointmentRepository.InsertAsync(appointment);

// Increment AppointmentsScheduled for the agent
        if (request.AgentId.HasValue && !string.IsNullOrEmpty(request.AgentId!.Value.ToString()))
        {
            var performanceIndicatorList = await _performanceIndicatorRepository.Find(pi => pi.AgentId == request.AgentId.ToString());

            PerformanceIndicator performanceIndicator;

            if (performanceIndicatorList == null || !performanceIndicatorList.Any())
            {
                // Create new performance indicator for the agent
                performanceIndicator = new PerformanceIndicator
                {
                    Id = Guid.NewGuid(),
                    AgentId = request.AgentId.ToString(),
                    LeadsGenerated = 0,
                    AppointmentsScheduled = 0,
                    SuccessfulSales = 0,
                    RecordedAt = DateTime.UtcNow
                };

                await _performanceIndicatorRepository.InsertAsync(performanceIndicator);
            }
            else
            {
                performanceIndicator = performanceIndicatorList.First();
                performanceIndicator.IncrementAppointmentsScheduled();
                await _performanceIndicatorRepository.Update(performanceIndicator);
            }
        }

        await _appointmentRepository.SaveAsync();

        return new CreateAppointmentResponse
        {
            AppointmentId = appointment.Id,
            Message = "Appointment created successfully."
        };
    }
}
