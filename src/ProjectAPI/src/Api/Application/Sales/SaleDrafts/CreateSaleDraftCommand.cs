using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Notary;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Infrastructure.Context;

// MediatR.Unit (its void marker) collides with the domain's Unit entity.
using UnitEntity = ProjectAPI.Domain.Immeubles.Entities.Unit;

namespace ProjectAPI.Api.Application.Sales.SaleDrafts;

/// <summary>
/// Opens a sale against an approved reservation (§5.7, §6).
///
/// This is the gated replacement for the old <c>POST /api/sales</c>, which was
/// removed because it wrote a Sale row from arbitrary request data — any unit,
/// any buyer, any price, no reservation, no final visit. The five preconditions
/// below are the whole reason this endpoint exists; a sale is not an
/// independent record you may create, it is the last step of one specific
/// dossier.
///
/// The buyer, the unit, the price and the deposit are all read from the
/// reservation. The client supplies only what it is allowed to decide: the
/// signature date, an agreed final price, and a note.
/// </summary>
public class CreateSaleDraftCommand : IRequest<SaleResponse>
{
    public Guid ReservationId { get; set; }

    /// <summary>Defaults to now. The notarial date is recorded later, at confirmation.</summary>
    public DateTime? SaleDate { get; set; }

    /// <summary>
    /// Agreed price. Defaults to the reservation's own final price (catalogue
    /// minus approved discount, §5.3) — it is not a free field: a different
    /// figure here is an agreed adjustment, not a new negotiation, and the
    /// validator keeps it positive.
    /// </summary>
    public decimal? FinalPrice { get; set; }

    public string? Notes { get; set; }
}

public class CreateSaleDraftValidator : AbstractValidator<CreateSaleDraftCommand>
{
    public CreateSaleDraftValidator()
    {
        RuleFor(x => x.ReservationId).NotEmpty();
        RuleFor(x => x.FinalPrice).GreaterThan(0).When(x => x.FinalPrice.HasValue);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public class CreateSaleDraftHandler : IRequestHandler<CreateSaleDraftCommand, SaleResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;
    private readonly NotaryEligibilityService _eligibility;
    private readonly ICurrentUser _currentUser;

    public CreateSaleDraftHandler(
        ApplicationDbContext db,
        ProjectScopeService projectScope,
        NotaryEligibilityService eligibility,
        ICurrentUser currentUser)
    {
        _db = db;
        _projectScope = projectScope;
        _eligibility = eligibility;
        _currentUser = currentUser;
    }

    public async Task<SaleResponse> Handle(CreateSaleDraftCommand request, CancellationToken ct)
    {
        var reservation = await _db.Set<Reservation>()
            .Include(r => r.PrimaryContact)
            .FirstOrDefaultAsync(r => r.Id == request.ReservationId, ct)
            ?? throw new NotFoundException($"Reservation {request.ReservationId} not found.");

        // §6.4 — perimeter first: everything below leaks information about a
        // dossier the caller may have no business seeing.
        await _projectScope.EnsureReservationAccessAsync(request.ReservationId, ct);

        // --- Precondition 1: the reservation is approved -----------------------
        // Anything else is either not yet decided (Pending/Draft/ChangesRequested),
        // refused, dead (Rejected/Cancelled/Expired), or already converted (Sold).
        if (reservation.Status != ReservationStatus.Approved)
        {
            throw BusinessRuleException.InvalidStatusTransition(
                reservation.Status.ToString(), "Sale(Draft)");
        }

        // --- Precondition 2: the project is in delivery ------------------------
        var project = await (
            from u in _db.Set<UnitEntity>()
            join im in _db.Set<Immeuble>() on u.ProjectId equals im.Id
            join p in _db.Set<Project>() on im.ProjectId equals p.Id
            where u.Id == reservation.UnitId
            select p).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(
                $"The project owning unit {reservation.UnitId} could not be resolved.");

        var phase = ProjectStatusCodes.GetBusinessPhase(project.StatusGlobal);
        if (phase != ProjectStatusCodes.Phase.EnLivraison)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.InvalidStatusTransition,
                $"Une vente ne peut être ouverte que sur un projet en livraison " +
                $"(phase actuelle : {phase}).",
                StatusCodes.Status409Conflict);
        }

        // --- Precondition 3: the final visit is cleared ------------------------
        await _eligibility.EnsureFinalVisitClearedAsync(request.ReservationId, ct);

        // --- Precondition 4: no active sale already ----------------------------
        // Checked on both axes because they can diverge: a sale created before
        // ReservationId existed carries a unit but no reservation. The filtered
        // unique indexes cover the race this read-then-write cannot.
        var activeStatuses = SaleStateMachine.ActiveStatuses;

        var alreadyOnReservation = await _db.Set<Sale>()
            .AnyAsync(s => s.ReservationId == request.ReservationId
                        && activeStatuses.Contains(s.Status), ct);
        if (alreadyOnReservation)
        {
            throw BusinessRuleException.SaleAlreadyExists();
        }

        var alreadyOnUnit = await _db.Set<Sale>()
            .AnyAsync(s => s.UnitId == reservation.UnitId
                        && activeStatuses.Contains(s.Status), ct);
        if (alreadyOnUnit)
        {
            throw BusinessRuleException.UnitNotAvailable(reservation.UnitId);
        }

        // --- Build the sale ----------------------------------------------------
        // The buyer snapshot comes from the reservation's own fields, falling
        // back to the linked CRM contact: §6.2 moved identity onto CrmContact,
        // but the inline fields are still populated on older files and are the
        // ones the buyer actually signed under.
        var contact = reservation.PrimaryContact;

        var firstName = FirstNonBlank(reservation.Name, contact?.FirstName);
        var lastName = FirstNonBlank(reservation.LastName, contact?.LastName);
        var email = FirstNonBlank(reservation.Email, contact?.Email);
        var phone = FirstNonBlank(reservation.PhoneNumber, contact?.Phone);

        // Sale requires all four. A reservation that reached APPROVED without an
        // identifiable buyer is a data problem upstream; say so instead of
        // writing empty strings into the record of a sale.
        if (firstName is null || lastName is null || email is null || phone is null)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                "La réservation ne porte pas d'identité acheteur complète " +
                "(nom, prénom, e-mail, téléphone) ; complétez-la avant d'ouvrir la vente.");
        }

        var finalPrice = request.FinalPrice
            ?? reservation.FinalPrice
            ?? reservation.TotalPropertyPrice;

        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            ReservationId = reservation.Id,
            UnitId = reservation.UnitId,
            Status = SaleStatus.Draft,

            BuyerId = reservation.BuyerId,
            BuyerFirstName = firstName,
            BuyerLastName = lastName,
            BuyerEmail = email,
            BuyerPhoneNumber = phone,
            BuyerCIN = FirstNonBlank(reservation.CIN, contact?.Cin),

            SaleDate = request.SaleDate ?? DateTime.UtcNow,

            // TotalPrice is the legacy column every existing read path uses; it
            // carries the agreed price so those reads stay correct.
            TotalPrice = finalPrice,
            FinalPrice = finalPrice,
            ReservationAmount = reservation.ReservationAmount,
            RemainingAmount = finalPrice - reservation.ReservationAmount,

            // §8 — frozen here. Changing the project's setting afterwards never
            // reaches a sale already opened.
            WarrantyMonths = project.WarrantyMonths,

            IsUnderConstruction = reservation.IsUnderConstruction,
            Notes = request.Notes,

            // Never from the request body: this is the record of who opened it.
            CreatedBy = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Add(sale);
        await _db.SaveChangesAsync(ct);

        return await SaleResponse.FromAsync(_db, sale, ct);
    }

    private static string? FirstNonBlank(params string?[] candidates) =>
        candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))?.Trim();
}
