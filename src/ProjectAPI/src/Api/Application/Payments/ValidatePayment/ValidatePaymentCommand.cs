using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Notifications;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Payments.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Payments.ValidatePayment;

/// <summary>
/// Moves a payment from PendingValidation to Validated (spec §14.3) — the
/// missing second step of the "declare, then confirm" workflow: a payment
/// recorded with ValidateImmediately=false had no way to ever leave
/// PendingValidation, which permanently excluded it from the buyer's totals.
/// </summary>
public class ValidatePaymentCommand : IRequest<ValidatePaymentResponse>
{
    public Guid PaymentId { get; set; }
    public string? ActorUserId { get; set; }
}

public class ValidatePaymentResponse
{
    public Guid PaymentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ValidatePaymentHandler : IRequestHandler<ValidatePaymentCommand, ValidatePaymentResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly PurchaseTotalsService _purchaseTotals;
    private readonly ProjectScopeService _projectScope;
    private readonly INotificationService _notifications;

    public ValidatePaymentHandler(
        ApplicationDbContext db,
        PurchaseTotalsService purchaseTotals,
        ProjectScopeService projectScope,
        INotificationService notifications)
    {
        _db = db;
        _purchaseTotals = purchaseTotals;
        _projectScope = projectScope;
        _notifications = notifications;
    }

    public async Task<ValidatePaymentResponse> Handle(ValidatePaymentCommand request, CancellationToken ct)
    {
        var payment = await _db.Set<Payment>()
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, ct)
            ?? throw new NotFoundException($"Payment {request.PaymentId} not found.");

        await _projectScope.EnsureReservationAccessAsync(payment.ReservationId, ct);

        if (payment.Status != PaymentStatus.PendingValidation)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.InvalidStatusTransition,
                $"Seul un paiement en attente de validation peut être validé (statut actuel : {payment.Status}).",
                StatusCodes.Status409Conflict);
        }

        payment.Status = PaymentStatus.Validated;
        payment.ValidatedBy = request.ActorUserId;
        payment.ValidatedAt = DateTime.UtcNow;

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        await _db.SaveChangesAsync(ct);
        await _purchaseTotals.RefreshForReservationAsync(payment.ReservationId, ct);
        await _db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);

        var reservation = await _db.Set<Reservation>().FirstOrDefaultAsync(r => r.Id == payment.ReservationId, ct);
        if (!string.IsNullOrWhiteSpace(reservation?.BuyerId))
        {
            await _notifications.NotifyAsync(
                reservation.BuyerId, "PAYMENT_VALIDATED",
                "Paiement validé",
                $"Votre paiement de {payment.Amount:N2} MAD a été validé.",
                payment.Id, "Payment", ct);
        }

        return new ValidatePaymentResponse
        {
            PaymentId = payment.Id,
            Status = payment.Status.ToString(),
            Message = "Paiement validé."
        };
    }
}
