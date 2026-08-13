using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Payments.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Payments.GetPaymentSchedule;

/// <summary>
/// Returns the active schedule with derived statuses and totals (spec §14.3
/// FR-PAY-005). Every figure is computed from validated entries — nothing is
/// read from a stored total (§5.8).
/// </summary>
public class GetPaymentScheduleQuery : IRequest<PaymentScheduleResponse>
{
    public Guid ReservationId { get; set; }
}

public class PaymentScheduleResponse
{
    public Guid? ScheduleId { get; set; }
    public int VersionNo { get; set; }
    public string Status { get; set; } = "None";
    public decimal ContractAmount { get; set; }
    public string Currency { get; set; } = "MAD";

    /// <summary>Sum of the non-cancelled installments.</summary>
    public decimal TotalCalled { get; set; }

    /// <summary>Net of validated payments (reversals included, being negative).</summary>
    public decimal TotalPaid { get; set; }

    /// <summary>Contract amount minus what has been paid.</summary>
    public decimal TotalRemaining { get; set; }

    /// <summary>Validated money not yet applied to an installment.</summary>
    public decimal UnallocatedCredit { get; set; }

    /// <summary>Recorded but not yet validated — excluded from TotalPaid (§14.3).</summary>
    public decimal PendingValidationAmount { get; set; }

    /// <summary>When these figures were computed (§5.8).</summary>
    public DateTime CalculatedAt { get; set; }

    public List<InstallmentDto> Installments { get; set; } = new();
    public List<PaymentDto> Payments { get; set; } = new();

    public class InstallmentDto
    {
        public Guid Id { get; set; }
        public int SequenceNo { get; set; }
        public string LabelFr { get; set; } = string.Empty;
        public string? LabelEn { get; set; }
        public decimal Percentage { get; set; }
        public decimal Amount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public DateTime DueDate { get; set; }

        /// <summary>Derived, never stored: UPCOMING / DUE / PARTIALLY_PAID / PAID / OVERDUE / CANCELLED.</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>Convenience flag for simple "paid / not yet" displays.</summary>
        public bool IsPaid { get; set; }
    }

    public class PaymentDto
    {
        public Guid Id { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string? MethodCode { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsReversal { get; set; }
        public string? Comment { get; set; }
    }
}

public class GetPaymentScheduleHandler : IRequestHandler<GetPaymentScheduleQuery, PaymentScheduleResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public GetPaymentScheduleHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db;
        _projectScope = projectScope;
    }

    public async Task<PaymentScheduleResponse> Handle(GetPaymentScheduleQuery request, CancellationToken ct)
    {
        // §6.4 — the buyer reads their own schedule (L propre); internal roles
        // are scoped to their assigned projects.
        await _projectScope.EnsureReservationAccessAsync(request.ReservationId, ct);
        await _projectScope.EnsureBuyerOwnsReservationAsync(request.ReservationId, ct);

        var now = DateTime.UtcNow;

        var payments = await _db.Set<Payment>()
            .Where(p => p.ReservationId == request.ReservationId)
            .ToListAsync(ct);

        var response = new PaymentScheduleResponse
        {
            CalculatedAt = now,
            TotalPaid = PaymentCalculator.TotalValidated(payments),
            PendingValidationAmount = payments
                .Where(p => p.Status == PaymentStatus.PendingValidation)
                .Sum(p => p.Amount),
            Payments = payments
                .OrderByDescending(p => p.PaymentDate)
                .Select(p => new PaymentScheduleResponse.PaymentDto
                {
                    Id = p.Id,
                    PaymentDate = p.PaymentDate,
                    Amount = p.Amount,
                    MethodCode = p.MethodCode,
                    Status = p.Status.ToString(),
                    IsReversal = p.ReversalOfPaymentId != null,
                    Comment = p.Comment
                })
                .ToList()
        };

        var schedule = await _db.Set<PaymentSchedule>()
            .Include(s => s.Installments)
                .ThenInclude(i => i.Allocations)
            .FirstOrDefaultAsync(
                s => s.ReservationId == request.ReservationId && s.Status == PaymentScheduleStatus.Active,
                ct);

        // Credits recorded against no installment. Uses the same "counts toward
        // totals" rule as PaymentCalculator so a reversed payment's credit is
        // withdrawn too.
        var countedIds = payments
            .Where(PaymentCalculator.CountsTowardTotals)
            .Select(p => p.Id)
            .ToHashSet();

        var reversedIds = payments
            .Where(p => p.ReversalOfPaymentId is not null && PaymentCalculator.CountsTowardTotals(p))
            .Select(p => p.ReversalOfPaymentId!.Value)
            .ToHashSet();

        var creditIds = countedIds.Except(reversedIds).ToList();

        response.UnallocatedCredit = await _db.Set<PaymentAllocation>()
            .Where(a => a.InstallmentId == null && creditIds.Contains(a.PaymentId))
            .SumAsync(a => (decimal?)a.AllocatedAmount, ct) ?? 0m;

        if (schedule is null)
        {
            // No active schedule yet: totals still reflect recorded payments.
            response.TotalRemaining = -response.TotalPaid;
            return response;
        }

        response.ScheduleId = schedule.Id;
        response.VersionNo = schedule.VersionNo;
        response.Status = schedule.Status.ToString();
        response.ContractAmount = schedule.ContractAmount;
        response.Currency = schedule.Currency;
        response.TotalCalled = PaymentCalculator.TotalCalled(schedule);
        response.TotalRemaining = schedule.ContractAmount - response.TotalPaid;

        response.Installments = schedule.Installments
            .OrderBy(i => i.SequenceNo)
            .Select(i =>
            {
                var paid = PaymentCalculator.PaidAmount(i, payments);
                var status = PaymentCalculator.StatusOf(i, payments, now);

                return new PaymentScheduleResponse.InstallmentDto
                {
                    Id = i.Id,
                    SequenceNo = i.SequenceNo,
                    LabelFr = i.LabelFr,
                    LabelEn = i.LabelEn,
                    Percentage = i.Percentage,
                    Amount = i.Amount,
                    PaidAmount = paid,
                    RemainingAmount = Math.Max(0, i.Amount - paid),
                    DueDate = i.DueDate,
                    Status = status.ToString(),
                    IsPaid = status == PaymentInstallmentStatus.Paid
                };
            })
            .ToList();

        return response;
    }
}
