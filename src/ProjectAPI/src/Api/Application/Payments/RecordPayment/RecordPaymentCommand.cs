using ProjectAPI.Api.Application.Common.Idempotency;

namespace ProjectAPI.Api.Application.Payments.RecordPayment;

/// <summary>
/// Records a payment the administration has received (spec §14.3 FR-PAY-003).
///
/// GPIA never collects money (§14.1): this only registers a declared receipt.
/// The entry is immutable once created — corrections go through a reversal.
///
/// §7 — requires an Idempotency-Key: a retried "record payment" call must not
/// create a duplicate ledger entry (MIN3 in the backend audit — previously
/// only ExternalReference uniqueness guarded against this, and that field is
/// optional).
/// </summary>
public class RecordPaymentCommand : IRequest<RecordPaymentResponse>, IIdempotentRequest
{
    public string? IdempotencyKey { get; set; }
    public Guid ReservationId { get; set; }

    public DateTime PaymentDate { get; set; }

    /// <summary>Must be strictly positive; a reversal is a separate command.</summary>
    public decimal Amount { get; set; }

    /// <summary>Declared channel: CASH, TRANSFER, PAYPAL, CMI… (see PaymentMethodCodes).</summary>
    public string? MethodCode { get; set; }

    /// <summary>Bank or internal reference; unique per source when provided.</summary>
    public string? ExternalReference { get; set; }

    public string? Comment { get; set; }

    /// <summary>User recording the entry.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Optional explicit split across installments. When omitted, the amount is
    /// applied oldest-installment-first; any surplus becomes an unallocated
    /// credit rather than being silently absorbed (§14.3).
    /// </summary>
    public List<AllocationInput>? Allocations { get; set; }

    /// <summary>Validate immediately (skips PENDING_VALIDATION).</summary>
    public bool ValidateImmediately { get; set; }

    public class AllocationInput
    {
        public Guid InstallmentId { get; set; }
        public decimal Amount { get; set; }
    }
}

public class RecordPaymentResponse
{
    public Guid PaymentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal AllocatedAmount { get; set; }
    public decimal UnallocatedAmount { get; set; }
    public string Message { get; set; } = string.Empty;
}
