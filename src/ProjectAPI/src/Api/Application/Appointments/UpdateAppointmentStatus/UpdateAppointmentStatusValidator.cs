namespace ProjectAPI.Api.Application.Appointments.UpdateAppointmentStatus;

/// <summary>
/// Validator for the <see cref="UpdateAppointmentStatusCommand"/> class.
/// </summary>
public class UpdateAppointmentStatusValidator : AbstractValidator<UpdateAppointmentStatusCommand>
{
    public UpdateAppointmentStatusValidator()
    {
        RuleFor(command => command.AppointmentId).NotEmpty().WithMessage("AppointmentId is required.");
        RuleFor(command => command.Status).NotEmpty().WithMessage("Status is required.")
            .Must(status => status == "Approved" || status == "Cancelled" || status == "Completed")
            .WithMessage("Status must be either 'Approved', 'Cancelled', or 'Completed'.");
    }
}
