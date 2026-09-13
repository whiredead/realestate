using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Sales.SaleDrafts;

/// <summary>
/// Corrects a sale that has not been finalised (§6).
///
/// Only the three fields the agent actually agreed are writable. The buyer, the
/// unit and the reservation are NOT: those came from the dossier, and a sale
/// that could be re-pointed at another unit or buyer after the fact would make
/// the one-active-sale-per-unit rule meaningless.
/// </summary>
public class UpdateSaleDraftCommand : IRequest<SaleResponse>
{
    public Guid SaleId { get; set; }
    public DateTime? SaleDate { get; set; }
    public decimal? FinalPrice { get; set; }
    public string? Notes { get; set; }
}

public class UpdateSaleDraftValidator : AbstractValidator<UpdateSaleDraftCommand>
{
    public UpdateSaleDraftValidator()
    {
        RuleFor(x => x.SaleId).NotEmpty();
        RuleFor(x => x.FinalPrice).GreaterThan(0).When(x => x.FinalPrice.HasValue);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public class UpdateSaleDraftHandler : IRequestHandler<UpdateSaleDraftCommand, SaleResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public UpdateSaleDraftHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db;
        _projectScope = projectScope;
    }

    public async Task<SaleResponse> Handle(UpdateSaleDraftCommand request, CancellationToken ct)
    {
        var sale = await _db.Set<Sale>()
            .FirstOrDefaultAsync(s => s.Id == request.SaleId, ct)
            ?? throw new NotFoundException($"Sale {request.SaleId} not found.");

        // A sale created before ReservationId existed is scoped by its unit.
        if (sale.ReservationId.HasValue)
        {
            await _projectScope.EnsureReservationAccessAsync(sale.ReservationId.Value, ct);
        }
        else
        {
            await _projectScope.EnsureUnitProjectAccessAsync(sale.UnitId, ct);
        }

        if (!SaleStateMachine.IsEditable(sale.Status))
        {
            throw BusinessRuleException.SaleNotEditable(sale.Status.ToString());
        }

        // Null means "leave as is" — a PATCH-shaped update, so clearing a note
        // is done with an empty string, not by omitting the field.
        if (request.SaleDate.HasValue)
        {
            sale.SaleDate = request.SaleDate.Value;
        }

        if (request.FinalPrice.HasValue)
        {
            sale.FinalPrice = request.FinalPrice.Value;

            // TotalPrice is what every legacy read path reports, so the two must
            // not drift; RemainingAmount is derived from the deposit snapshot
            // taken at creation, never recomputed from the payment ledger (that
            // is PaymentTracking's job and it answers a different question).
            sale.TotalPrice = request.FinalPrice.Value;
            sale.RemainingAmount = request.FinalPrice.Value - (sale.ReservationAmount ?? 0m);
        }

        if (request.Notes is not null)
        {
            sale.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes;
        }

        await _db.SaveChangesAsync(ct);

        return await SaleResponse.FromAsync(_db, sale, ct);
    }
}
