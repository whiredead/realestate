using Microsoft.AspNetCore.Identity;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Appointments.Interfaces;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Api.Application.Notary.Appointments.GetNotaryAppointmentAssignmentHistory;

public class GetNotaryAppointmentAssignmentHistoryHandler
    : IRequestHandler<GetNotaryAppointmentAssignmentHistoryQuery, List<NotaryAppointmentAssignmentHistoryDto>>
{
    private readonly INotaryAppointmentRepository _appointmentRepository;
    private readonly INotaryAppointmentAssignmentHistoryRepository _historyRepository;
    private readonly UserManager<User> _userManager;

    public GetNotaryAppointmentAssignmentHistoryHandler(
        INotaryAppointmentRepository appointmentRepository,
        INotaryAppointmentAssignmentHistoryRepository historyRepository,
        UserManager<User> userManager)
    {
        _appointmentRepository = appointmentRepository;
        _historyRepository = historyRepository;
        _userManager = userManager;
    }

    public async Task<List<NotaryAppointmentAssignmentHistoryDto>> Handle(GetNotaryAppointmentAssignmentHistoryQuery request, CancellationToken cancellationToken)
    {
        var appointmentIds = new List<Guid>();
        var currentId = (Guid?)request.NotaryAppointmentId;
        var guard = 0;

        while (currentId.HasValue && guard++ < 50)
        {
            var appointment = await _appointmentRepository.GetByIDAsync(currentId.Value);
            if (appointment == null) break;

            appointmentIds.Add(currentId.Value);
            currentId = appointment.PreviousAppointmentId;
        }

        if (appointmentIds.Count == 0)
        {
            throw new NotFoundException($"Notary appointment with ID {request.NotaryAppointmentId} not found.");
        }

        var allHistory = (await _historyRepository.Find(h => appointmentIds.Contains(h.NotaryAppointmentId)))
            .OrderBy(h => h.AssignedAt)
            .ToList();

        if (!string.IsNullOrWhiteSpace(request.RestrictToNotaryId))
        {
            var involved = allHistory.Any(h =>
                h.NotaireId == request.RestrictToNotaryId ||
                h.PreviousNotaireId == request.RestrictToNotaryId);

            if (!involved)
            {
                throw new BusinessRuleException(
                    BusinessErrorCodes.Unauthorized,
                    "Cet historique n'est pas accessible.",
                    StatusCodes.Status403Forbidden);
            }
        }

        var notaryIds = allHistory
            .SelectMany(h => new[] { h.NotaireId, h.PreviousNotaireId })
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        var notaryNames = new Dictionary<string, string>();
        foreach (var notaryId in notaryIds)
        {
            var user = await _userManager.FindByIdAsync(notaryId!);
            if (user != null)
            {
                notaryNames[notaryId!] = $"{user.FirstName} {user.LastName}";
            }
        }

        return allHistory
            .Select(h => new NotaryAppointmentAssignmentHistoryDto
            {
                Id = h.Id,
                NotaireId = h.NotaireId,
                NotaireFullName = h.NotaireId != null && notaryNames.TryGetValue(h.NotaireId, out var name1) ? name1 : null,
                PreviousNotaireId = h.PreviousNotaireId,
                PreviousNotaireFullName = h.PreviousNotaireId != null && notaryNames.TryGetValue(h.PreviousNotaireId, out var name2) ? name2 : null,
                AssignmentSource = h.AssignmentSource,
                Reason = h.Reason,
                ActorUserId = h.ActorUserId,
                AssignedAt = h.AssignedAt
            })
            .ToList();
    }
}
