namespace ProjectAPI.Api.Application.Payments.CreatePaymentSchedule;

/// <summary>
/// Creates a payment schedule for a reservation (spec §14.2 FR-PAY-001).
/// The schedule is created as DRAFT; it only takes effect once activated.
/// </summary>
public class CreatePaymentScheduleCommand : IRequest<CreatePaymentScheduleResponse>
{
    public Guid ReservationId { get; set; }

    /// <summary>Contract amount the installments must add up to.</summary>
    public decimal ContractAmount { get; set; }

    public string Currency { get; set; } = "MAD";

    /// <summary>Schedule this one revises, if any (§14.2 versioning).</summary>
    public Guid? SupersedesId { get; set; }

    public List<InstallmentInput> Installments { get; set; } = new();

    /// <summary>Activate immediately after creation, if the 100 % rule is satisfied.</summary>
    public bool ActivateImmediately { get; set; }

    public class InstallmentInput
    {
        public int SequenceNo { get; set; }
        public string LabelFr { get; set; } = string.Empty;
        public string? LabelEn { get; set; }
        public decimal Percentage { get; set; }
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public string? Comment { get; set; }
    }
}

public class CreatePaymentScheduleResponse
{
    public Guid ScheduleId { get; set; }
    public int VersionNo { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
