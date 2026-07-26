using FluentValidation;
using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.FinalVisits.Entities;

namespace ProjectAPI.Api.Application.NotaryAppointments.UpdateNotaryAppointment;

public class UpdateNotaryAppointmentValidator : AbstractValidator<UpdateNotaryAppointmentCommand>
{
    public UpdateNotaryAppointmentValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");

        // Statuses come from the shared appointment lifecycle (§47.3), reused by
        // final-visit and notary appointments alike — see AppointmentAttemptStatus.
        RuleFor(x => x.Status)
            .Must(status => string.IsNullOrEmpty(status) || Enum.TryParse<AppointmentAttemptStatus>(status, out _))
            .WithMessage(
                "Status must be one of: " +
                string.Join(", ", Enum.GetNames(typeof(AppointmentAttemptStatus))) + ".");

        // §5.7 — the outcome vocabulary is fixed and distinct from the status one.
        RuleFor(x => x.Outcome)
            .Must(outcome => string.IsNullOrEmpty(outcome) || NotaryOutcomeCodes.TryParse(outcome, out _))
            .WithMessage("Outcome must be one of: " + string.Join(", ", NotaryOutcomeCodes.All) + ".");

        RuleFor(x => x.OutcomeNote)
            .MaximumLength(2000);

        RuleFor(x => x.TaxFees)
            .GreaterThanOrEqualTo(0)
            .When(x => x.TaxFees.HasValue)
            .WithMessage("TaxFees must be greater than or equal to 0.");

        RuleFor(x => x.TahfidFees)
            .GreaterThanOrEqualTo(0)
            .When(x => x.TahfidFees.HasValue)
            .WithMessage("TahfidFees must be greater than or equal to 0.");
    }
}
