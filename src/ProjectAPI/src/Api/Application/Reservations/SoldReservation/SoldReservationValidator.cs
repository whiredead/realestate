using FluentValidation;

namespace ProjectAPI.Api.Application.Reservations.SoldReservation;

public class SoldReservationValidator : AbstractValidator<SoldReservationCommand>
{
    public SoldReservationValidator()
    {
        RuleFor(x => x.ReservationId)
            .NotEmpty()
            .WithMessage("ReservationId is required.");
    }
}