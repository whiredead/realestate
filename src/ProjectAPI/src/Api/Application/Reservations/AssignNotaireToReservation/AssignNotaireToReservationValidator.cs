using FluentValidation;

namespace ProjectAPI.Api.Application.Reservations.AssignNotaireToReservation;

public class AssignNotaireToReservationValidator : AbstractValidator<AssignNotaireToReservationCommand>
{
    public AssignNotaireToReservationValidator()
    {
        RuleFor(x => x.ReservationId)
            .NotEmpty()
            .WithMessage("ReservationId is required.");
    }
}