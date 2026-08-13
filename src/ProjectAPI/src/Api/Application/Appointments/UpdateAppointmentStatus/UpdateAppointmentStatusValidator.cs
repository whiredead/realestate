using ProjectAPI.Domain.FinalVisits.Entities;

namespace ProjectAPI.Api.Application.Appointments.UpdateAppointmentStatus;

/// <summary>
/// Validator for the <see cref="UpdateAppointmentStatusCommand"/> class.
///
/// Was checking Status against "Approved"/"Cancelled"/"Completed" — none of
/// which is a real AppointmentAttemptStatus name (the handler parses
/// Confirmed/RescheduleProposed/Rejected/NoShow via Enum.TryParse), so most
/// valid transitions were rejected by this validator before ever reaching
/// the handler. Now validates against the real enum, case-insensitively.
/// </summary>
public class UpdateAppointmentStatusValidator : AbstractValidator<UpdateAppointmentStatusCommand>
{
    public UpdateAppointmentStatusValidator()
    {
        RuleFor(command => command.AppointmentId).NotEmpty().WithMessage("AppointmentId is required.");
        RuleFor(command => command.Status).NotEmpty().WithMessage("Status is required.")
            .Must(status => Enum.TryParse<AppointmentAttemptStatus>(status, ignoreCase: true, out _))
            .WithMessage("Status must be a valid appointment status (Requested, Confirmed, RescheduleProposed, Rejected, Cancelled, Completed, NoShow, Superseded).");
    }
}
