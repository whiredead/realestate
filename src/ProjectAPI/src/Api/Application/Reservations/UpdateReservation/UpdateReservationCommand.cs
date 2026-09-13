using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Reservations.UpdateReservation;

/// <summary>
/// Corrects a reservation that has not been decided yet (§12.2).
///
/// This is what makes CHANGES_REQUESTED mean anything: an administrator could
/// already ask an agent to fix a dossier, and the agent could resubmit it — but
/// there was no way to actually change a single field in between, so the only
/// honest response to "the CIN is wrong" was to cancel and start again, losing
/// the file's history.
///
/// Editable in DRAFT and CHANGES_REQUESTED only. A SUBMITTED file is in front
/// of an administrator and must not shift under them; an APPROVED one has been
/// decided, and its price is what the buyer agreed to.
///
/// The unit is NOT editable. Moving a reservation to a different unit is a
/// different file: it would have to release one hold and take another, past the
/// availability checks that only run at creation and submission.
/// </summary>
public class UpdateReservationCommand : IRequest<UpdateReservationResponse>
{
    public Guid ReservationId { get; set; }

    // Null means "leave unchanged" throughout.
    public string? Name { get; set; }
    public string? LastName { get; set; }
    public string? CIN { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }

    public decimal? TotalPropertyPrice { get; set; }
    public decimal? ReservationAmount { get; set; }

    /// <summary>§5.3 — changing the discount recomputes FinalPrice from the frozen catalogue price.</summary>
    public decimal? Discount { get; set; }

    public string? NotaireId { get; set; }
}

public class UpdateReservationResponse
{
    public Guid ReservationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? FinalPrice { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class UpdateReservationValidator : AbstractValidator<UpdateReservationCommand>
{
    public UpdateReservationValidator()
    {
        RuleFor(x => x.ReservationId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150).When(x => x.Name is not null);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(150).When(x => x.LastName is not null);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.CIN).MaximumLength(50).When(x => x.CIN is not null);
        RuleFor(x => x.PhoneNumber).MaximumLength(50).When(x => x.PhoneNumber is not null);

        RuleFor(x => x.TotalPropertyPrice).GreaterThan(0).When(x => x.TotalPropertyPrice.HasValue);
        RuleFor(x => x.ReservationAmount).GreaterThanOrEqualTo(0).When(x => x.ReservationAmount.HasValue);
        RuleFor(x => x.Discount).GreaterThanOrEqualTo(0).When(x => x.Discount.HasValue);
    }
}

public class UpdateReservationHandler : IRequestHandler<UpdateReservationCommand, UpdateReservationResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public UpdateReservationHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db;
        _projectScope = projectScope;
    }

    public async Task<UpdateReservationResponse> Handle(UpdateReservationCommand request, CancellationToken ct)
    {
        await _projectScope.EnsureReservationAccessAsync(request.ReservationId, ct);

        var reservation = await _db.Set<Reservation>()
            .FirstOrDefaultAsync(r => r.Id == request.ReservationId, ct)
            ?? throw new NotFoundException($"Reservation {request.ReservationId} not found.");

        if (reservation.Status is not (ReservationStatus.Draft or ReservationStatus.ChangesRequested))
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.InvalidStatusTransition,
                "Seule une réservation en brouillon ou en attente de correction peut être modifiée " +
                $"(statut actuel : {reservation.Status}).",
                StatusCodes.Status409Conflict);
        }

        reservation.Name = request.Name ?? reservation.Name;
        reservation.LastName = request.LastName ?? reservation.LastName;
        reservation.CIN = request.CIN ?? reservation.CIN;
        reservation.Email = request.Email ?? reservation.Email;
        reservation.PhoneNumber = request.PhoneNumber ?? reservation.PhoneNumber;
        reservation.NotaireId = request.NotaireId ?? reservation.NotaireId;

        reservation.TotalPropertyPrice = request.TotalPropertyPrice ?? reservation.TotalPropertyPrice;
        reservation.ReservationAmount = request.ReservationAmount ?? reservation.ReservationAmount;

        if (request.Discount.HasValue)
        {
            reservation.Discount = request.Discount.Value;
        }

        // §5.3 — final_price = catalog_price - discount. CatalogPrice stays
        // frozen at whatever it was at creation, so a catalogue move since then
        // does not silently reprice a file under negotiation; only the discount
        // and the price the agent actually typed move.
        var catalogPrice = reservation.CatalogPrice ?? reservation.TotalPropertyPrice;
        reservation.FinalPrice = catalogPrice - reservation.Discount;

        if (reservation.FinalPrice < 0)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                $"La remise ({reservation.Discount:N2}) dépasse le prix catalogue ({catalogPrice:N2}).");
        }

        await _db.SaveChangesAsync(ct);

        return new UpdateReservationResponse
        {
            ReservationId = reservation.Id,
            Status = reservation.Status.ToString(),
            FinalPrice = reservation.FinalPrice,
            Message = "Réservation mise à jour."
        };
    }
}
