using FluentValidation;

namespace ProjectAPI.Api.Application.Reservations.RejectReservation;

/// <summary>§5.3 "Reject ⇒ reason mandatory" — no validator existed for this command at all, so a reject could previously proceed with Reason null/empty.</summary>
public class RejectReservationValidator : AbstractValidator<RejectReservationCommand>
{
    public RejectReservationValidator()
    {
        RuleFor(x => x.ReservationId)
            .NotEmpty()
            .WithMessage("ReservationId is required.");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Un motif est obligatoire pour rejeter une réservation.");
    }
}
