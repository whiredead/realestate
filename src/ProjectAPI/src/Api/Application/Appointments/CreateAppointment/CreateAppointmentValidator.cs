namespace ProjectAPI.Api.Application.Appointments.CreateAppointment;
public class CreateAppointmentValidator : AbstractValidator<CreateAppointmentCommand>
{
    public CreateAppointmentValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty().WithMessage("ProjectId is required.");
        //RuleFor(x => x.AgentId).NotEmpty().WithMessage("AgentId is required.");
        RuleFor(x => x.AppointmentDate).GreaterThan(DateTime.Now).WithMessage("Appointment date must be in the future.");

        // Validation for guests (unauthenticated users)
        When(x => string.IsNullOrEmpty(x.UserId), () =>
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required for unauthenticated users.");
            RuleFor(x => x.Email).NotEmpty().WithMessage("Email is required for unauthenticated users.")
                .EmailAddress().WithMessage("A valid email address is required.");
            RuleFor(x => x.PhoneNumber).NotEmpty().WithMessage("Phone number is required for unauthenticated users.");
        });
    }
}
