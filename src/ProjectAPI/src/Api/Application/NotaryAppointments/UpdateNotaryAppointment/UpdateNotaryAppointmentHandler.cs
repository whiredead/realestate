using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Appointments.Interfaces;
using ProjectAPI.Domain.FinalVisits.Entities;

namespace ProjectAPI.Api.Application.NotaryAppointments.UpdateNotaryAppointment;

/// <summary>
/// Updates a notary appointment (spec §18.4 FR-NOT-004).
///
/// Status changes are gated by <see cref="AppointmentStateMachine"/> — the same
/// matrix used for final-visit appointments (§47.3) — instead of accepting any
/// string. A notary appointment can no longer jump directly from, say,
/// `Requested` to `Completed` without going through `Confirmed` first.
/// </summary>
public class UpdateNotaryAppointmentHandler : IRequestHandler<UpdateNotaryAppointmentCommand, UpdateNotaryAppointmentResponse>
{
    private readonly INotaryAppointmentRepository _notaryAppointmentRepository;

    public UpdateNotaryAppointmentHandler(INotaryAppointmentRepository notaryAppointmentRepository)
    {
        _notaryAppointmentRepository = notaryAppointmentRepository;
    }

    public async Task<UpdateNotaryAppointmentResponse> Handle(UpdateNotaryAppointmentCommand request, CancellationToken cancellationToken)
    {
        var notaryAppointment = await _notaryAppointmentRepository.GetByIDAsync(request.Id)
            ?? throw new NotFoundException($"Notary appointment with ID {request.Id} not found.");

        var hasUpdates = false;

        if (request.Status != null)
        {
            // The validator already rejects unknown names; a parse failure here
            // would mean the persisted value predates this enum (legacy data).
            if (!Enum.TryParse<AppointmentAttemptStatus>(notaryAppointment.Status, out var currentStatus))
            {
                currentStatus = AppointmentAttemptStatus.Requested;
            }

            var targetStatus = Enum.Parse<AppointmentAttemptStatus>(request.Status);

            if (currentStatus != targetStatus && !AppointmentStateMachine.CanTransition(currentStatus, targetStatus))
            {
                throw BusinessRuleException.InvalidStatusTransition(currentStatus.ToString(), targetStatus.ToString());
            }

            notaryAppointment.Status = targetStatus.ToString();
            hasUpdates = true;
        }

        if (request.TaxFees.HasValue)
        {
            notaryAppointment.TaxFees = request.TaxFees.Value;
            hasUpdates = true;
        }

        if (request.TahfidFees.HasValue)
        {
            notaryAppointment.TahfidFees = request.TahfidFees.Value;
            hasUpdates = true;
        }

        if (!hasUpdates)
        {
            return new UpdateNotaryAppointmentResponse
            {
                Success = false,
                Message = "No fields provided for update."
            };
        }

        await _notaryAppointmentRepository.SaveAsync();

        return new UpdateNotaryAppointmentResponse
        {
            Success = true,
            Message = "Notary appointment updated successfully."
        };
    }
}
