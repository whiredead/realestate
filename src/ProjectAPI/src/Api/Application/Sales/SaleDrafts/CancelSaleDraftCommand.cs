using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Sales.SaleDrafts;

/// <summary>
/// Abandons a sale before it is finalised (§6, §9).
///
/// The row is NOT deleted — §9 forbids hard-deleting business data, and the
/// abandonment is itself part of the dossier's history. Cancelled falls outside
/// the filtered unique indexes, so cancelling frees both the reservation and
/// the unit for a fresh sale.
///
/// A Confirmed sale is refused here: undoing a notarial act is not a
/// cancellation, and there is no transition out of Confirmed at all.
/// </summary>
public class CancelSaleDraftCommand : IRequest<SaleResponse>
{
    public Guid SaleId { get; set; }

    /// <summary>Appended to the sale's notes so the reason survives the status change.</summary>
    public string? Reason { get; set; }
}

public class CancelSaleDraftValidator : AbstractValidator<CancelSaleDraftCommand>
{
    public CancelSaleDraftValidator()
    {
        RuleFor(x => x.SaleId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public class CancelSaleDraftHandler : IRequestHandler<CancelSaleDraftCommand, SaleResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;
    private readonly ICurrentUser _currentUser;

    public CancelSaleDraftHandler(
        ApplicationDbContext db,
        ProjectScopeService projectScope,
        ICurrentUser currentUser)
    {
        _db = db;
        _projectScope = projectScope;
        _currentUser = currentUser;
    }

    public async Task<SaleResponse> Handle(CancelSaleDraftCommand request, CancellationToken ct)
    {
        var sale = await _db.Set<Sale>()
            .FirstOrDefaultAsync(s => s.Id == request.SaleId, ct)
            ?? throw new NotFoundException($"Sale {request.SaleId} not found.");

        if (sale.ReservationId.HasValue)
        {
            await _projectScope.EnsureReservationAccessAsync(sale.ReservationId.Value, ct);
        }
        else
        {
            await _projectScope.EnsureUnitProjectAccessAsync(sale.UnitId, ct);
        }

        // Throws InvalidSaleTransitionException -> 409 INVALID_STATUS_TRANSITION
        // for Confirmed, and for an already-cancelled sale.
        SaleStateMachine.EnsureCanTransition(sale.Status, SaleStatus.Cancelled);

        var cancelledAt = DateTime.UtcNow;
        var trace =
            $"[Annulée le {cancelledAt:yyyy-MM-dd HH:mm} UTC par {_currentUser.UserId ?? "?"}]" +
            (string.IsNullOrWhiteSpace(request.Reason) ? "" : $" {request.Reason}");

        sale.Notes = string.IsNullOrWhiteSpace(sale.Notes) ? trace : $"{sale.Notes}\n{trace}";
        sale.Status = SaleStatus.Cancelled;

        await _db.SaveChangesAsync(ct);

        return await SaleResponse.FromAsync(_db, sale, ct);
    }
}
