using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Payments.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Payments.RejectPayment;

/// <summary>
/// Moves a payment from PendingValidation to Rejected (spec §14.3) — the
/// other missing half of the "declare, then confirm/refuse" workflow. A
/// rejected entry is never counted in totals and, unlike a reversal, is not
/// itself a ledger movement (nothing was ever confirmed received).
/// </summary>
public class RejectPaymentCommand : IRequest<RejectPaymentResponse>
{
    public Guid PaymentId { get; set; }

    /// <summary>Why the declared receipt is refused. Mandatory for auditability.</summary>
    public string Reason { get; set; } = string.Empty;

    public string? ActorUserId { get; set; }
}

public class RejectPaymentResponse
{
    public Guid PaymentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class RejectPaymentHandler : IRequestHandler<RejectPaymentCommand, RejectPaymentResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public RejectPaymentHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db;
        _projectScope = projectScope;
    }

    public async Task<RejectPaymentResponse> Handle(RejectPaymentCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                "Un motif est obligatoire pour rejeter un paiement.");
        }

        var payment = await _db.Set<Payment>()
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, ct)
            ?? throw new NotFoundException($"Payment {request.PaymentId} not found.");

        await _projectScope.EnsureReservationAccessAsync(payment.ReservationId, ct);

        if (payment.Status != PaymentStatus.PendingValidation)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.InvalidStatusTransition,
                $"Seul un paiement en attente de validation peut être rejeté (statut actuel : {payment.Status}).",
                StatusCodes.Status409Conflict);
        }

        payment.Status = PaymentStatus.Rejected;
        payment.Comment = string.IsNullOrWhiteSpace(payment.Comment)
            ? request.Reason
            : $"{payment.Comment} | Rejeté : {request.Reason}";

        await _db.SaveChangesAsync(ct);

        return new RejectPaymentResponse
        {
            PaymentId = payment.Id,
            Status = payment.Status.ToString(),
            Message = "Paiement rejeté."
        };
    }
}
