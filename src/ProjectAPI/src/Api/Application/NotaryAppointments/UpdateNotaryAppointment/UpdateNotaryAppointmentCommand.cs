namespace ProjectAPI.Api.Application.NotaryAppointments.UpdateNotaryAppointment;

public class UpdateNotaryAppointmentCommand : IRequest<UpdateNotaryAppointmentResponse>
{
    public Guid Id { get; set; }
    public string? Status { get; set; }

    /// <summary>
    /// §5.7 outcome code, distinct from <see cref="Status"/>. Mandatory when
    /// moving the appointment to Completed: the outcome is what decides whether
    /// the sale converts, so completing without one is refused.
    /// </summary>
    public string? Outcome { get; set; }

    /// <summary>Explanation for a non-completing outcome.</summary>
    public string? OutcomeNote { get; set; }

    public decimal? TaxFees { get; set; }
    public decimal? TahfidFees { get; set; }

    /// <summary>Reassigns to a different eligible notary. Required together with <see cref="ReassignmentReason"/>.</summary>
    public string? NewNotaireId { get; set; }

    /// <summary>Required when NewNotaireId is supplied.</summary>
    public string? ReassignmentReason { get; set; }

    public string? ActorUserId { get; set; }
}