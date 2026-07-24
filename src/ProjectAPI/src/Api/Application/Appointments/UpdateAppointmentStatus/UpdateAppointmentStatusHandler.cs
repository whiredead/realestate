using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Appointments.Interfaces;

namespace ProjectAPI.Api.Application.Appointments.UpdateAppointmentStatus;

/// <summary>
/// Handler for updating the status of an appointment.
/// </summary>
/// <remarks>
/// This handler processes the <see cref="UpdateAppointmentStatusCommand" /> to update the status of an appointment.
/// It retrieves the appointment from the repository and updates its status as requested.
/// </remarks>
public class UpdateAppointmentStatusHandler : IRequestHandler<UpdateAppointmentStatusCommand, UpdateAppointmentStatusResponse>
{
    private readonly IAppointmentRepository _appointmentRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAppointmentStatusHandler" /> class.
    /// </summary>
    /// <param name="appointmentRepository">The repository to access appointment data.</param>
    public UpdateAppointmentStatusHandler(IAppointmentRepository appointmentRepository)
    {
        _appointmentRepository = appointmentRepository;
    }

    /// <summary>
    /// Handles the command to update an appointment status.
    /// </summary>
    /// <param name="request">The <see cref="UpdateAppointmentStatusCommand" /> containing the appointment ID and new status.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation, containing the <see cref="UpdateAppointmentStatusResponse" />.</returns>
    /// <exception cref="NotFoundException">Thrown when an appointment with the specified ID does not exist.</exception>
    public async Task<UpdateAppointmentStatusResponse> Handle(UpdateAppointmentStatusCommand request, CancellationToken cancellationToken)
    {
        // Retrieve the appointment from the repository
        var appointment = await _appointmentRepository.GetByIDAsync(request.AppointmentId);
        if (appointment == null)
        {
            throw new NotFoundException($"Appointment with ID {request.AppointmentId} not found.");
        }

        // Update the appointment status
        appointment.Status = request.Status;
        _appointmentRepository.Update(appointment);
        await _appointmentRepository.SaveAsync();

        // Create response
        return new UpdateAppointmentStatusResponse
        {
            AppointmentId = appointment.Id,
            Status = appointment.Status,
            Message = "Appointment status updated successfully."
        };
    }
}