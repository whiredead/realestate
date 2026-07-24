namespace ProjectAPI.Api.Application.Appointments.GetAppointments;

/// <summary>
/// Validator for the <see cref="GetAppointmentsQuery"/> class.
/// </summary>
public class GetAppointmentsValidator : AbstractValidator<GetAppointmentsQuery>
{
    public GetAppointmentsValidator()
    {
       // RuleFor(query => query.AgentId).MaximumLength(450);
        RuleFor(query => query.Status).MaximumLength(100);
        RuleFor(query => query.Name).MaximumLength(150);
        RuleFor(query => query.LastName).MaximumLength(150);
        RuleFor(query => query.Email).MaximumLength(150).EmailAddress();
        RuleFor(query => query.PhoneNumber).MaximumLength(20);
    }
}