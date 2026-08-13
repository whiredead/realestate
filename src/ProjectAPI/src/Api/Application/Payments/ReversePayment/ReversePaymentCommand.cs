using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Payments.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Payments.ReversePayment;

/// <summary>
/// Reverses a validated payment (contrepassation) — spec §14.3, §5.4.
///
/// A financial entry is immutable: an error is never fixed by editing the
/// amount. A mirror entry with the opposite sign is recorded and linked back
/// through <c>ReversalOfPaymentId</c>, and the original is marked REVERSED.
/// Totals then recompute correctly because they sum signed validated entries.
/// </summary>
public class ReversePaymentCommand : IRequest<ReversePaymentResponse>
{
    public Guid PaymentId { get; set; }

    /// <summary>Why the entry is being reversed. Mandatory for auditability.</summary>
    public string Reason { get; set; } = string.Empty;

    public string? ActorUserId { get; set; }
}

public class ReversePaymentResponse
{
    public Guid ReversalPaymentId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ReversePaymentHandler : IRequestHandler<ReversePaymentCommand, ReversePaymentResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly PurchaseTotalsService _purchaseTotals;
    private readonly ProjectScopeService _projectScope;

    public ReversePaymentHandler(ApplicationDbContext db, PurchaseTotalsService purchaseTotals, ProjectScopeService projectScope)
    {
        _db = db;
        _purchaseTotals = purchaseTotals;
        _projectScope = projectScope;
    }

    public async Task<ReversePaymentResponse> Handle(ReversePaymentCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                "Un motif est obligatoire pour une contrepassation.");
        }

        var original = await _db.Set<Payment>()
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, ct)
            ?? throw new NotFoundException($"Payment {request.PaymentId} not found.");

        // §6.4 — reversal is admin-only and project-scoped.
        await _projectScope.EnsureReservationAccessAsync(original.ReservationId, ct);

        if (original.Status == PaymentStatus.Reversed)
        {
            throw new BusinessRuleException(
                "PAYMENT_ALREADY_REVERSED",
                "Ce paiement a déjà été contrepassé.",
                StatusCodes.Status409Conflict);
        }

        if (original.Status != PaymentStatus.Validated)
        {
            // Only a validated entry affects totals, so only it needs reversing.
            // A pending or rejected entry is corrected by rejecting it.
            throw new BusinessRuleException(
                BusinessErrorCodes.InvalidStatusTransition,
                $"Seul un paiement validé peut être contrepassé (statut actuel : {original.Status}).",
                StatusCodes.Status409Conflict);
        }

        if (original.ReversalOfPaymentId is not null)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                "Une contrepassation ne peut pas être contrepassée.");
        }

        var reversal = new Payment
        {
            Id = Guid.NewGuid(),
            ReservationId = original.ReservationId,
            PaymentDate = DateTime.UtcNow,
            Amount = -original.Amount, // signed: cancels the original in any sum
            Currency = original.Currency,
            MethodCode = original.MethodCode,
            ExternalReference = null, // avoid colliding with the original's unique reference
            Source = original.Source,
            Status = PaymentStatus.Validated,
            ReversalOfPaymentId = original.Id,
            ValidatedBy = request.ActorUserId,
            ValidatedAt = DateTime.UtcNow,
            Comment = request.Reason,
            CreatedBy = request.ActorUserId,
            CreatedAt = DateTime.UtcNow
        };

        original.Status = PaymentStatus.Reversed;

        _db.Add(reversal);

        // Ledger write and cached-total refresh commit together (§5.7).
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        await _db.SaveChangesAsync(ct);
        await _purchaseTotals.RefreshForReservationAsync(original.ReservationId, ct);
        await _db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);

        return new ReversePaymentResponse
        {
            ReversalPaymentId = reversal.Id,
            Message = "Contrepassation enregistrée."
        };
    }
}
