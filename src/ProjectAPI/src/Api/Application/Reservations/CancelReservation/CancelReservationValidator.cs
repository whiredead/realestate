using FluentValidation;

namespace ProjectAPI.Api.Application.Reservations.CancelReservation;

public class CancelReservationValidator : AbstractValidator<CancelReservationCommand>
{
    public CancelReservationValidator()
    {
        RuleFor(x => x.ReservationId)
            .NotEmpty()
            .WithMessage("ReservationId is required.");
    }
}